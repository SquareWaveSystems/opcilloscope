# Opcilloscope uninstaller for Windows
# Usage: irm https://raw.githubusercontent.com/SquareWaveSystems/opcilloscope/main/uninstall.ps1 | iex

$ErrorActionPreference = "Stop"

$InstallDir = if ($env:OPCILLOSCOPE_INSTALL_DIR) { $env:OPCILLOSCOPE_INSTALL_DIR } else { "$env:LOCALAPPDATA\Opcilloscope" }
$ConfigDir = "$env:APPDATA\opcilloscope"
$CertDir = "$env:LOCALAPPDATA\opcilloscope"

function Write-Info { param($Message) Write-Host "[INFO] $Message" -ForegroundColor Green }
function Write-Warn { param($Message) Write-Host "[WARN] $Message" -ForegroundColor Yellow }

function Uninstall-Opcilloscope {
    Write-Host ""
    Write-Host "  +===================================+" -ForegroundColor Cyan
    Write-Host "  |    Opcilloscope Uninstaller       |" -ForegroundColor Cyan
    Write-Host "  |   Terminal OPC UA Client          |" -ForegroundColor Cyan
    Write-Host "  +===================================+" -ForegroundColor Cyan
    Write-Host ""

    $removedSomething = $false
    $exePath = Join-Path $InstallDir "opcilloscope.exe"

    # Remove binary / install directory
    if (Test-Path $exePath) {
        Remove-Item $InstallDir -Recurse -Force
        Write-Info "Removed install directory: $InstallDir"
        $removedSomething = $true
    } elseif (Test-Path $InstallDir) {
        Remove-Item $InstallDir -Recurse -Force
        Write-Info "Removed install directory: $InstallDir"
        $removedSomething = $true
    } else {
        Write-Warn "Install directory not found at $InstallDir"
    }

    # Remove from PATH
    $userPath = [Environment]::GetEnvironmentVariable("Path", "User")
    if ($userPath -and $userPath -like "*$InstallDir*") {
        $newPath = ($userPath -split ";" | Where-Object { $_ -ne $InstallDir }) -join ";"
        [Environment]::SetEnvironmentVariable("Path", $newPath, "User")
        Write-Info "Removed $InstallDir from user PATH"
    }

    # Prompt to remove config directory
    if (Test-Path $ConfigDir) {
        $interactive = [Environment]::UserInteractive -and -not ([Console]::IsInputRedirected)
        if ($interactive) {
            $answer = Read-Host "Remove configuration directory $ConfigDir? [y/N]"
            if ($answer -eq "y" -or $answer -eq "Y") {
                Remove-Item $ConfigDir -Recurse -Force
                Write-Info "Removed config directory: $ConfigDir"
            } else {
                Write-Info "Kept config directory: $ConfigDir"
            }
        } else {
            Write-Info "Kept config directory: $ConfigDir (run interactively to remove)"
        }
    }

    # Prompt to remove certificate directory
    if (Test-Path $CertDir) {
        $interactive = [Environment]::UserInteractive -and -not ([Console]::IsInputRedirected)
        if ($interactive) {
            $answer = Read-Host "Remove OPC UA certificates directory $CertDir? [y/N]"
            if ($answer -eq "y" -or $answer -eq "Y") {
                Remove-Item $CertDir -Recurse -Force
                Write-Info "Removed certificates directory: $CertDir"
            } else {
                Write-Info "Kept certificates directory: $CertDir"
            }
        } else {
            Write-Info "Kept certificates directory: $CertDir (run interactively to remove)"
        }
    }

    Write-Host ""
    if ($removedSomething) {
        Write-Info "Opcilloscope has been uninstalled."
        Write-Host "(You may need to restart your terminal for PATH changes to take effect)" -ForegroundColor Gray
    } else {
        Write-Warn "Opcilloscope does not appear to be installed at $InstallDir."
        Write-Host ""
        Write-Host 'If you installed to a custom directory, set $env:OPCILLOSCOPE_INSTALL_DIR first:' -ForegroundColor White
        Write-Host '  $env:OPCILLOSCOPE_INSTALL_DIR = "C:\your\path"; .\uninstall.ps1' -ForegroundColor White
    }
}

Uninstall-Opcilloscope
