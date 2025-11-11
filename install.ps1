# ShipvillanWin Installer Script
# Usage: irm https://raw.githubusercontent.com/eng-bf/shipvillan-win/main/install.ps1 | iex

param(
    [string]$Version = "latest",
    [switch]$Force
)

$ErrorActionPreference = "Stop"

$AppName = "ShipvillanWin"
$GitHubRepo = "eng-bf/shipvillan-win"
$InstallPath = Join-Path $env:LOCALAPPDATA $AppName

Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "   ShipvillanWin Installer" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host ""

# Function to get latest release info from GitHub
function Get-LatestRelease {
    try {
        Write-Host "Fetching latest release info from GitHub..." -ForegroundColor Yellow
        $release = Invoke-RestMethod -Uri "https://api.github.com/repos/$GitHubRepo/releases/latest"
        return $release
    }
    catch {
        Write-Error "Failed to fetch release info: $_"
        exit 1
    }
}

# Function to download file with progress
function Download-File {
    param(
        [string]$Url,
        [string]$OutFile
    )

    try {
        Write-Host "Downloading from: $Url" -ForegroundColor Yellow
        $ProgressPreference = 'SilentlyContinue'
        Invoke-WebRequest -Uri $Url -OutFile $OutFile -UseBasicParsing
        $ProgressPreference = 'Continue'
        Write-Host "Downloaded to: $OutFile" -ForegroundColor Green
    }
    catch {
        Write-Error "Failed to download file: $_"
        exit 1
    }
}

# Check if app is already running
Write-Host "Checking if $AppName is running..." -ForegroundColor Yellow
$runningProcess = Get-Process -Name $AppName -ErrorAction SilentlyContinue

if ($runningProcess -and !$Force) {
    Write-Host ""
    Write-Host "ERROR: $AppName is currently running." -ForegroundColor Red
    Write-Host "Please close the application and try again, or use -Force to kill the process." -ForegroundColor Red
    Write-Host ""
    exit 1
}

if ($runningProcess -and $Force) {
    Write-Host "Force stopping $AppName..." -ForegroundColor Yellow
    Stop-Process -Name $AppName -Force
    Start-Sleep -Seconds 2
}

# Get release information
$release = Get-LatestRelease
$version = $release.tag_name
Write-Host ""
Write-Host "Latest version: $version" -ForegroundColor Green

# Find Setup.exe in release assets
$setupAsset = $release.assets | Where-Object { $_.name -eq "Setup.exe" }

if (!$setupAsset) {
    Write-Error "Setup.exe not found in release assets"
    exit 1
}

# Create temporary directory
$tempDir = Join-Path $env:TEMP "ShipvillanWin-Install"
if (Test-Path $tempDir) {
    Remove-Item $tempDir -Recurse -Force
}
New-Item -ItemType Directory -Path $tempDir -Force | Out-Null

# Download Setup.exe
$setupPath = Join-Path $tempDir "Setup.exe"
Download-File -Url $setupAsset.browser_download_url -OutFile $setupPath

Write-Host ""
Write-Host "Preparing installation..." -ForegroundColor Yellow

# Add Windows Defender exclusion to prevent false positive warnings
try {
    Write-Host "Adding Windows Defender exclusion for Setup.exe..." -ForegroundColor Yellow
    Add-MpPreference -ExclusionPath $setupPath -ErrorAction Stop
    Write-Host "Windows Defender exclusion added successfully." -ForegroundColor Green
}
catch {
    Write-Host "Note: Could not add Windows Defender exclusion (requires admin privileges)." -ForegroundColor Yellow
    Write-Host "If Windows Defender blocks the installer, click 'More info' then 'Run anyway'." -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Installing $AppName..." -ForegroundColor Yellow

# Run Setup.exe silently
try {
    $installProcess = Start-Process -FilePath $setupPath -ArgumentList "--silent" -Wait -PassThru -NoNewWindow

    if ($installProcess.ExitCode -eq 0) {
        Write-Host ""
        Write-Host "=====================================" -ForegroundColor Green
        Write-Host "   Installation Successful!" -ForegroundColor Green
        Write-Host "=====================================" -ForegroundColor Green
        Write-Host ""
        Write-Host "$AppName has been installed and will start automatically." -ForegroundColor Green
        Write-Host "The application will:" -ForegroundColor Cyan
        Write-Host "  - Run in the system tray" -ForegroundColor Cyan
        Write-Host "  - Start automatically at login" -ForegroundColor Cyan
        Write-Host "  - Auto-update when new versions are released" -ForegroundColor Cyan
        Write-Host ""
        Write-Host "Right-click the tray icon to configure." -ForegroundColor Yellow
    }
    else {
        Write-Error "Installation failed with exit code: $($installProcess.ExitCode)"
        exit 1
    }
}
catch {
    Write-Error "Installation failed: $_"
    exit 1
}
finally {
    # Cleanup Windows Defender exclusion
    if (Test-Path $setupPath) {
        Remove-MpPreference -ExclusionPath $setupPath -ErrorAction SilentlyContinue
    }

    # Cleanup temporary files
    if (Test-Path $tempDir) {
        Remove-Item $tempDir -Recurse -Force -ErrorAction SilentlyContinue
    }
}

Write-Host ""
Write-Host "Installation complete!" -ForegroundColor Green
