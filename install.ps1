# OpcScope installer for Windows
# Usage: irm https://raw.githubusercontent.com/BrettKinny/OpcScope/main/install.ps1 | iex

$ErrorActionPreference = "Stop"

$Repo = "BrettKinny/OpcScope"
$InstallDir = if ($env:OPCSCOPE_INSTALL_DIR) { $env:OPCSCOPE_INSTALL_DIR } else { "$env:LOCALAPPDATA\OpcScope" }

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

function Install-OpcScope {
    Write-Host ""
    Write-Host "  +===================================+" -ForegroundColor Cyan
    Write-Host "  |       OpcScope Installer          |" -ForegroundColor Cyan
    Write-Host "  |   Terminal OPC UA Client          |" -ForegroundColor Cyan
    Write-Host "  +===================================+" -ForegroundColor Cyan
    Write-Host ""

    Write-Info "Detecting platform..."
    $platform = Get-Platform
    Write-Info "Platform: $platform"

    Write-Info "Fetching latest version..."
    $version = Get-LatestVersion
    Write-Info "Version: $version"

    $downloadUrl = "https://github.com/$Repo/releases/download/$version/opcscope-$platform.zip"
    Write-Info "Downloading from: $downloadUrl"

    $tempDir = Join-Path $env:TEMP "opcscope-install"
    $zipPath = Join-Path $tempDir "opcscope.zip"

    # Cleanup and create temp directory
    if (Test-Path $tempDir) { Remove-Item -Recurse -Force $tempDir }
    New-Item -ItemType Directory -Path $tempDir | Out-Null

    try {
        # Download
        Invoke-WebRequest -Uri $downloadUrl -OutFile $zipPath -UseBasicParsing

        Write-Info "Extracting..."
        Expand-Archive -Path $zipPath -DestinationPath $tempDir -Force

        Write-Info "Installing to $InstallDir..."
        if (-not (Test-Path $InstallDir)) {
            New-Item -ItemType Directory -Path $InstallDir | Out-Null
        }

        # Move executable
        $exePath = Get-ChildItem -Path $tempDir -Filter "*.exe" -Recurse | Select-Object -First 1
        if ($exePath) {
            Copy-Item -Path $exePath.FullName -Destination (Join-Path $InstallDir "opcscope.exe") -Force
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

        Write-Info "OpcScope $version installed successfully!"
        Write-Host ""
        Write-Host "Run 'opcscope' to start the application." -ForegroundColor White
        Write-Host "(You may need to restart your terminal for PATH changes to take effect)" -ForegroundColor Gray

    } finally {
        # Cleanup
        if (Test-Path $tempDir) { Remove-Item -Recurse -Force $tempDir }
    }
}

Install-OpcScope
