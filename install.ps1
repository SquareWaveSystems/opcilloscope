# Opcilloscope installer for Windows
# Usage: irm https://raw.githubusercontent.com/SquareWaveSystems/opcilloscope/main/install.ps1 | iex

$ErrorActionPreference = "Stop"

$Repo = "SquareWaveSystems/opcilloscope"
$InstallDir = if ($env:OPCILLOSCOPE_INSTALL_DIR) { $env:OPCILLOSCOPE_INSTALL_DIR } else { "$env:LOCALAPPDATA\Opcilloscope" }

function Write-Info { param($Message) Write-Host "[INFO] $Message" -ForegroundColor Green }
function Write-Warn { param($Message) Write-Host "[WARN] $Message" -ForegroundColor Yellow }
function Write-Err { param($Message) Write-Host "[ERROR] $Message" -ForegroundColor Red; exit 1 }

function Get-Platform {
    $arch = if ([Environment]::Is64BitOperatingSystem) {
        if ($env:PROCESSOR_ARCHITECTURE -eq "ARM64") { "arm64" } else { "x64" }
    } else {
        Write-Err "32-bit Windows is not supported"
    }
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

function Install-Opcilloscope {
    Write-Host ""
    Write-Host "  +===================================+" -ForegroundColor Cyan
    Write-Host "  |     Opcilloscope Installer        |" -ForegroundColor Cyan
    Write-Host "  |   Terminal OPC UA Client          |" -ForegroundColor Cyan
    Write-Host "  +===================================+" -ForegroundColor Cyan
    Write-Host ""

    Write-Info "Detecting platform..."
    $platform = Get-Platform
    Write-Info "Platform: $platform"

    Write-Info "Fetching latest version..."
    $version = Get-LatestVersion
    Write-Info "Version: $version"

    $downloadUrl = "https://github.com/$Repo/releases/download/$version/opcilloscope-$platform.zip"
    Write-Info "Downloading from: $downloadUrl"

    $tempDir = Join-Path $env:TEMP "opcilloscope-install"
    $zipPath = Join-Path $tempDir "opcilloscope.zip"

    # Cleanup and create temp directory
    if (Test-Path $tempDir) { Remove-Item -Recurse -Force $tempDir }
    New-Item -ItemType Directory -Path $tempDir | Out-Null

    try {
        # Download
        Invoke-WebRequest -Uri $downloadUrl -OutFile $zipPath -UseBasicParsing

        # Verify the archive against the SHA256SUMS published with the release.
        # Degrades gracefully (warning only) when SHA256SUMS is unavailable (older releases).
        $archiveName = "opcilloscope-$platform.zip"
        $sumsUrl = "https://github.com/$Repo/releases/download/$version/SHA256SUMS"
        $sumsPath = Join-Path $tempDir "SHA256SUMS"
        $haveSums = $true
        try {
            Invoke-WebRequest -Uri $sumsUrl -OutFile $sumsPath -UseBasicParsing
        } catch {
            $haveSums = $false
            Write-Warn "SHA256SUMS not found for $version (older release?). Skipping checksum verification."
        }
        if ($haveSums) {
            Write-Info "Verifying checksum..."
            $entry = Get-Content $sumsPath | Where-Object { $_ -match ("\s" + [regex]::Escape($archiveName) + "$") } | Select-Object -First 1
            if (-not $entry) {
                Write-Warn "No checksum entry for $archiveName in SHA256SUMS. Skipping checksum verification."
            } else {
                $expected = ($entry -split '\s+')[0].ToLowerInvariant()
                $actual = (Get-FileHash -Path $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
                if ($actual -ne $expected) {
                    Write-Err "Checksum mismatch for ${archiveName}. Expected $expected but got $actual. The download may be corrupted or tampered with. Aborting."
                }
                Write-Info "Checksum verified (SHA-256)."
            }
        }

        Write-Info "Extracting..."
        Expand-Archive -Path $zipPath -DestinationPath $tempDir -Force

        Write-Info "Installing to $InstallDir..."
        if (-not (Test-Path $InstallDir)) {
            New-Item -ItemType Directory -Path $InstallDir | Out-Null
        }

        # Move executable
        $exePath = Get-ChildItem -Path $tempDir -Filter "*.exe" -Recurse | Select-Object -First 1
        if ($exePath) {
            Copy-Item -Path $exePath.FullName -Destination (Join-Path $InstallDir "opcilloscope.exe") -Force
        } else {
            Write-Err "Could not find executable in archive"
        }

        # Add to PATH if not already there
        $userPath = [Environment]::GetEnvironmentVariable("Path", "User")
        if ($userPath -notlike "*$InstallDir*") {
            Write-Info "Adding $InstallDir to user PATH..."
            [Environment]::SetEnvironmentVariable("Path", "$userPath;$InstallDir", "User")
            $env:Path = "$env:Path;$InstallDir"
        }

        Write-Info "Opcilloscope $version installed successfully!"
        Write-Host ""
        Write-Host "Run 'opcilloscope' to start the application." -ForegroundColor White
        Write-Host "(You may need to restart your terminal for PATH changes to take effect)" -ForegroundColor Gray

    } finally {
        # Cleanup
        if (Test-Path $tempDir) { Remove-Item -Recurse -Force $tempDir }
    }
}

Install-Opcilloscope
