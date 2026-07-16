# opcilloscope uninstaller for Windows
# Usage: irm https://raw.githubusercontent.com/SquareWaveSystems/opcilloscope/main/uninstall.ps1 | iex

$ErrorActionPreference = "Stop"

$UsingCustomInstallDir = -not [string]::IsNullOrWhiteSpace($env:OPCILLOSCOPE_INSTALL_DIR)
$InstallDir = if ($UsingCustomInstallDir) {
    $env:OPCILLOSCOPE_INSTALL_DIR
} else {
    Join-Path $env:LOCALAPPDATA "Programs\opcilloscope"
}
$LicenseDir = Join-Path $InstallDir "opcilloscope-licenses"
$LegacyInstallDir = Join-Path $env:LOCALAPPDATA "Opcilloscope"
$ConfigDir = Join-Path $env:APPDATA "opcilloscope"
$CertificateDir = Join-Path $env:LOCALAPPDATA "opcilloscope\pki"

function Write-Info { param($Message) Write-Host "[INFO] $Message" -ForegroundColor Green }
function Write-Warn { param($Message) Write-Host "[WARN] $Message" -ForegroundColor Yellow }

function Normalize-PathEntry {
    param([string]$Path)
    if ([string]::IsNullOrWhiteSpace($Path)) { return "" }
    $expanded = [Environment]::ExpandEnvironmentVariables($Path.Trim().Trim('"'))
    return $expanded.TrimEnd('\').TrimEnd('/')
}

function Remove-UserPathEntry {
    param([string]$Entry)
    $userPath = [Environment]::GetEnvironmentVariable("Path", "User")
    if ([string]::IsNullOrEmpty($userPath)) { return $false }

    $target = Normalize-PathEntry $Entry
    $originalEntries = @($userPath -split ";" | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    $newEntries = @($originalEntries | Where-Object { (Normalize-PathEntry $_) -ine $target })
    if ($newEntries.Count -eq $originalEntries.Count) { return $false }

    [Environment]::SetEnvironmentVariable("Path", ($newEntries -join ";"), "User")
    return $true
}

function Confirm-Removal {
    param([string]$Prompt, [bool]$Interactive)
    if (-not $Interactive) {
        Write-Info "$Prompt skipped (run interactively to remove retained user data)"
        return $false
    }

    $answer = Read-Host "$Prompt [y/N]"
    return $answer -eq "y" -or $answer -eq "Y"
}

function Remove-DirectoryIfEmpty {
    param([string]$Path)
    if ((Test-Path $Path) -and -not (Get-ChildItem -Path $Path -Force | Select-Object -First 1)) {
        Remove-Item $Path -Force
    }
}

function Uninstall-Opcilloscope {
    Write-Host ""
    Write-Host "  +===================================+" -ForegroundColor Cyan
    Write-Host "  |    opcilloscope uninstaller       |" -ForegroundColor Cyan
    Write-Host "  |   terminal OPC UA client          |" -ForegroundColor Cyan
    Write-Host "  +===================================+" -ForegroundColor Cyan
    Write-Host ""

    $removedSomething = $false
    $interactive = [Environment]::UserInteractive -and -not ([Console]::IsInputRedirected)
    $exePath = Join-Path $InstallDir "opcilloscope.exe"

    # InstallDir may be a shared custom directory. Remove only known app files.
    if (Test-Path $exePath) {
        Remove-Item $exePath -Force
        Write-Info "Removed executable: $exePath"
        $removedSomething = $true
    } else {
        Write-Warn "Executable not found at $exePath"
    }

    if (Test-Path $LicenseDir) {
        Remove-Item $LicenseDir -Recurse -Force
        Write-Info "Removed license notices: $LicenseDir"
        $removedSomething = $true
    }

    if (-not $UsingCustomInstallDir) {
        if (Remove-UserPathEntry $InstallDir) {
            Write-Info "Removed $InstallDir from user PATH"
        }

        # Legacy releases put the executable in the same case-insensitive parent
        # used for certificates. Remove only the known executable and PATH entry.
        $legacyExe = Join-Path $LegacyInstallDir "opcilloscope.exe"
        if (Test-Path $legacyExe) {
            Remove-Item $legacyExe -Force
            Write-Info "Removed legacy executable: $legacyExe"
            $removedSomething = $true
        }
        if (Remove-UserPathEntry $LegacyInstallDir) {
            Write-Info "Removed legacy path entry: $LegacyInstallDir"
        }
        Remove-DirectoryIfEmpty $InstallDir
    }

    if (Test-Path $ConfigDir) {
        if (Confirm-Removal "Remove configuration directory $ConfigDir?" $interactive) {
            Remove-Item $ConfigDir -Recurse -Force
            Write-Info "Removed configuration directory: $ConfigDir"
            $removedSomething = $true
        } else {
            Write-Info "Kept configuration directory: $ConfigDir"
        }
    }

    if (Test-Path $CertificateDir) {
        if (Confirm-Removal "Remove OPC UA certificate store $CertificateDir?" $interactive) {
            Remove-Item $CertificateDir -Recurse -Force
            Write-Info "Removed certificate store: $CertificateDir"
            $removedSomething = $true
            Remove-DirectoryIfEmpty $LegacyInstallDir
        } else {
            Write-Info "Kept certificate store: $CertificateDir"
        }
    }

    Write-Host ""
    if ($removedSomething) {
        Write-Info "opcilloscope has been uninstalled."
        Write-Host "(Restart open terminals to pick up PATH changes.)" -ForegroundColor Gray
    } else {
        Write-Warn "opcilloscope does not appear to be installed at $InstallDir."
        Write-Host ""
        Write-Host 'If you installed to a custom directory, set $env:OPCILLOSCOPE_INSTALL_DIR first:' -ForegroundColor White
        Write-Host '  $env:OPCILLOSCOPE_INSTALL_DIR = "C:\your\path"; .\uninstall.ps1' -ForegroundColor White
    }
}

Uninstall-Opcilloscope
