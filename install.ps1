# opcilloscope installer for Windows
# Usage: irm https://raw.githubusercontent.com/SquareWaveSystems/opcilloscope/main/install.ps1 | iex

$ErrorActionPreference = "Stop"

$Repo = "SquareWaveSystems/opcilloscope"
$UsingCustomInstallDir = -not [string]::IsNullOrWhiteSpace($env:OPCILLOSCOPE_INSTALL_DIR)
$InstallDir = if ($UsingCustomInstallDir) {
    $env:OPCILLOSCOPE_INSTALL_DIR
} else {
    Join-Path $env:LOCALAPPDATA "Programs\opcilloscope"
}
$LicenseDir = Join-Path $InstallDir "opcilloscope-licenses"

# Releases before v1 installed here. On Windows this path is also the app's
# case-insensitive certificate-data parent, so migrate only the known exe.
$LegacyInstallDir = Join-Path $env:LOCALAPPDATA "Opcilloscope"

function Write-Info { param($Message) Write-Host "[INFO] $Message" -ForegroundColor Green }
function Write-Warn { param($Message) Write-Host "[WARN] $Message" -ForegroundColor Yellow }
function Write-Err { param($Message) Write-Host "[ERROR] $Message" -ForegroundColor Red; throw $Message }

function Normalize-PathEntry {
    param([string]$Path)
    if ([string]::IsNullOrWhiteSpace($Path)) { return "" }
    $expanded = [Environment]::ExpandEnvironmentVariables($Path.Trim().Trim('"'))
    return $expanded.TrimEnd('\').TrimEnd('/')
}

function Test-UserPathEntry {
    param([string]$UserPath, [string]$Entry)
    $target = Normalize-PathEntry $Entry
    return @($UserPath -split ";" | Where-Object {
        (Normalize-PathEntry $_) -ieq $target
    }).Count -gt 0
}

function Remove-UserPathEntry {
    param([string]$Entry)
    $userPath = [Environment]::GetEnvironmentVariable("Path", "User")
    if ([string]::IsNullOrEmpty($userPath)) { return }

    $target = Normalize-PathEntry $Entry
    $entries = @($userPath -split ";" | Where-Object {
        -not [string]::IsNullOrWhiteSpace($_) -and
        (Normalize-PathEntry $_) -ine $target
    })
    [Environment]::SetEnvironmentVariable("Path", ($entries -join ";"), "User")
}

function Get-Platform {
    if (-not [Environment]::Is64BitOperatingSystem) {
        Write-Err "32-bit Windows is not supported"
    }

    $architecture = if ($env:PROCESSOR_ARCHITEW6432) {
        $env:PROCESSOR_ARCHITEW6432
    } else {
        $env:PROCESSOR_ARCHITECTURE
    }
    $arch = if ($architecture -eq "ARM64") { "arm64" } else { "x64" }
    return "win-$arch"
}

function Get-LatestVersion {
    try {
        $response = Invoke-RestMethod -Uri "https://api.github.com/repos/$Repo/releases/latest" -UseBasicParsing
        return $response.tag_name
    } catch {
        Write-Err "Could not fetch latest version: $_"
    }
}

function Install-LicenseMaterial {
    param([string]$ExtractedRoot)

    # This app-specific directory is installer-owned. Replace it so notices
    # removed by a package upgrade cannot linger from an older release.
    if (Test-Path $LicenseDir) {
        Remove-Item $LicenseDir -Recurse -Force
    }
    New-Item -ItemType Directory -Path $LicenseDir -Force | Out-Null
    $copied = $false

    $projectLicense = Join-Path $ExtractedRoot "LICENSE"
    if (Test-Path $projectLicense) {
        Copy-Item $projectLicense (Join-Path $LicenseDir "LICENSE.txt") -Force
        $copied = $true
    }

    $aggregateNotices = Join-Path $ExtractedRoot "THIRD-PARTY-NOTICES.md"
    if (Test-Path $aggregateNotices) {
        Copy-Item $aggregateNotices $LicenseDir -Force
        $copied = $true
    }

    $noticeDirectory = Join-Path $ExtractedRoot "licenses"
    if (Test-Path $noticeDirectory) {
        Copy-Item (Join-Path $noticeDirectory "*") $LicenseDir -Recurse -Force
        $copied = $true
    }

    if ($copied) {
        Write-Info "Installed license notices to $LicenseDir"
    } else {
        Write-Warn "This older release archive did not contain license notice files."
        Remove-Item $LicenseDir -Force -ErrorAction SilentlyContinue
    }
}

function Install-Opcilloscope {
    Write-Host ""
    Write-Host "  +===================================+" -ForegroundColor Cyan
    Write-Host "  |     opcilloscope installer        |" -ForegroundColor Cyan
    Write-Host "  |   terminal OPC UA client          |" -ForegroundColor Cyan
    Write-Host "  +===================================+" -ForegroundColor Cyan
    Write-Host ""

    Write-Info "Detecting platform..."
    $platform = Get-Platform
    Write-Info "Platform: $platform"

    Write-Info "Fetching latest version..."
    $version = Get-LatestVersion
    Write-Info "Version: $version"

    $archiveName = "opcilloscope-$platform.zip"
    $downloadUrl = "https://github.com/$Repo/releases/download/$version/$archiveName"
    Write-Info "Downloading from: $downloadUrl"

    $tempDir = Join-Path $env:TEMP "opcilloscope-install-$PID"
    $zipPath = Join-Path $tempDir $archiveName
    New-Item -ItemType Directory -Path $tempDir -Force | Out-Null

    try {
        Invoke-WebRequest -Uri $downloadUrl -OutFile $zipPath -UseBasicParsing

        # The installer targets the latest release. Current releases are
        # required to publish a checksum entry for every archive.
        $sumsUrl = "https://github.com/$Repo/releases/download/$version/SHA256SUMS"
        $sumsPath = Join-Path $tempDir "SHA256SUMS"
        try {
            Invoke-WebRequest -Uri $sumsUrl -OutFile $sumsPath -UseBasicParsing
        } catch {
            Write-Err "Could not download SHA256SUMS for $version. Refusing an unverified install: $_"
        }

        Write-Info "Verifying checksum..."
        $entry = Get-Content $sumsPath |
            Where-Object { $_ -match ("\s" + [regex]::Escape($archiveName) + "$") } |
            Select-Object -First 1
        if (-not $entry) {
            Write-Err "SHA256SUMS has no checksum entry for $archiveName. Refusing an unverified install."
        }

        $expected = ($entry -split '\s+')[0].ToLowerInvariant()
        if ($expected -notmatch '^[0-9a-f]{64}$') {
            Write-Err "SHA256SUMS contains an invalid checksum for $archiveName."
        }
        $actual = (Get-FileHash -Path $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($actual -ne $expected) {
            Write-Err "Checksum mismatch for $archiveName. Expected $expected but got $actual. Aborting."
        }
        Write-Info "Checksum verified (SHA-256)."

        Write-Info "Extracting..."
        Expand-Archive -Path $zipPath -DestinationPath $tempDir -Force

        $exePath = Get-ChildItem -Path $tempDir -Filter "opcilloscope.exe" -File -Recurse |
            Select-Object -First 1
        if (-not $exePath) {
            Write-Err "Could not find opcilloscope.exe in archive"
        }

        Write-Info "Installing to $InstallDir..."
        New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
        Copy-Item $exePath.FullName (Join-Path $InstallDir "opcilloscope.exe") -Force
        Install-LicenseMaterial $tempDir

        # Safely migrate the legacy default: remove only the old executable.
        if (-not $UsingCustomInstallDir) {
            $legacyExe = Join-Path $LegacyInstallDir "opcilloscope.exe"
            if (Test-Path $legacyExe) {
                Remove-Item $legacyExe -Force
                Write-Info "Removed legacy executable: $legacyExe"
            }
            Remove-UserPathEntry $LegacyInstallDir
        }

        $installedExe = Join-Path $InstallDir "opcilloscope.exe"
        & $installedExe --help | Out-Null
        if ($LASTEXITCODE -ne 0) {
            Write-Err "Installed executable failed its command-line smoke test"
        }

        $userPath = [Environment]::GetEnvironmentVariable("Path", "User")
        if (-not $UsingCustomInstallDir) {
            if (-not (Test-UserPathEntry $userPath $InstallDir)) {
                Write-Info "Adding $InstallDir to user PATH..."
                $entries = @($userPath -split ";" | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
                [Environment]::SetEnvironmentVariable("Path", (@($entries) + $InstallDir) -join ";", "User")
            }
            if (-not (Test-UserPathEntry $env:Path $InstallDir)) {
                $env:Path = "$env:Path;$InstallDir"
            }
        } elseif (-not (Test-UserPathEntry $env:Path $InstallDir)) {
            Write-Warn "Custom install directory is not in PATH; PATH was left unchanged."
        }

        Write-Info "opcilloscope $version installed successfully!"
        Write-Host ""
        Write-Host "Run 'opcilloscope' to start the application." -ForegroundColor White
        Write-Host "(You may need to restart other terminals for PATH changes to take effect)" -ForegroundColor Gray
    } finally {
        if (Test-Path $tempDir) { Remove-Item -Recurse -Force $tempDir }
    }
}

Install-Opcilloscope
