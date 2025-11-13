# ShipvillanWin Build Script
# Builds both x86 and x64 versions, but auto-updates only work for x64
#
# Usage: .\build-release.ps1 -Version 1.0.1

param(
    [Parameter(Mandatory=$true)]
    [string]$Version
)

$ErrorActionPreference = "Stop"

$ProjectPath = "src\ShipvillanWin\ShipvillanWin.csproj"
$SquirrelExe = ".\tools\squirrel.exe"
$GithubReleaseDir = ".\github-release"

Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "   ShipvillanWin Build Script" -ForegroundColor Cyan
Write-Host "   Version: $Version" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host ""

# Verify Squirrel tools exist
if (!(Test-Path $SquirrelExe)) {
    Write-Host "ERROR: Squirrel tools not found!" -ForegroundColor Red
    Write-Host "Run: .\download-squirrel.ps1" -ForegroundColor Yellow
    exit 1
}

# Clean previous build outputs
Write-Host "Cleaning previous build outputs..." -ForegroundColor Yellow
if (Test-Path ".\publish-x86") { Remove-Item ".\publish-x86" -Recurse -Force }
if (Test-Path ".\publish-x64") { Remove-Item ".\publish-x64" -Recurse -Force }
if (Test-Path ".\releases-x86") { Remove-Item ".\releases-x86" -Recurse -Force }
if (Test-Path ".\releases-x64") { Remove-Item ".\releases-x64" -Recurse -Force }
if (Test-Path $GithubReleaseDir) { Remove-Item $GithubReleaseDir -Recurse -Force }

Write-Host ""
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "Building x86 (32-bit) Version" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host ""

# Build x86 version
Write-Host "Publishing x86 binary..." -ForegroundColor Yellow
dotnet publish $ProjectPath `
    -c Release `
    -r win-x86 `
    --self-contained false `
    -o .\publish-x86 `
    -p:Version=$Version `
    -p:AssemblyVersion=$Version `
    -p:FileVersion=$Version

if ($LASTEXITCODE -ne 0) {
    Write-Error "x86 build failed!"
    exit 1
}

# Package x86 with Squirrel
Write-Host "Packaging x86 with Squirrel..." -ForegroundColor Yellow
& $SquirrelExe pack `
    --packId ShipvillanWin-x86 `
    --packVersion $Version `
    --packDirectory .\publish-x86 `
    --releaseDir .\releases-x86 `
    --allowUnaware

if ($LASTEXITCODE -ne 0) {
    Write-Error "x86 packaging failed!"
    exit 1
}

Write-Host "x86 build complete!" -ForegroundColor Green
Write-Host ""

Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "Building x64 (64-bit) Version" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host ""

# Build x64 version
Write-Host "Publishing x64 binary..." -ForegroundColor Yellow
dotnet publish $ProjectPath `
    -c Release `
    -r win-x64 `
    --self-contained false `
    -o .\publish-x64 `
    -p:Version=$Version `
    -p:AssemblyVersion=$Version `
    -p:FileVersion=$Version

if ($LASTEXITCODE -ne 0) {
    Write-Error "x64 build failed!"
    exit 1
}

# Package x64 with Squirrel (use "ShipvillanWin" as package ID for auto-updates)
Write-Host "Packaging x64 with Squirrel..." -ForegroundColor Yellow
& $SquirrelExe pack `
    --packId ShipvillanWin `
    --packVersion $Version `
    --packDirectory .\publish-x64 `
    --releaseDir .\releases-x64 `
    --allowUnaware

if ($LASTEXITCODE -ne 0) {
    Write-Error "x64 packaging failed!"
    exit 1
}

Write-Host "x64 build complete!" -ForegroundColor Green
Write-Host ""

# Create GitHub release folder with all files
Write-Host "Preparing GitHub release folder..." -ForegroundColor Yellow
New-Item -ItemType Directory -Path $GithubReleaseDir -Force | Out-Null

# Copy x86 files
Write-Host "Copying x86 files..." -ForegroundColor Yellow
Copy-Item ".\releases-x86\ShipvillanWin-x86-$Version-full.nupkg" $GithubReleaseDir -Force
if (Test-Path ".\releases-x86\ShipvillanWin-x86Setup.exe") {
    Copy-Item ".\releases-x86\ShipvillanWin-x86Setup.exe" "$GithubReleaseDir\Setup-x86.exe" -Force
}

# Copy x64 files
Write-Host "Copying x64 files..." -ForegroundColor Yellow
Copy-Item ".\releases-x64\ShipvillanWin-$Version-full.nupkg" $GithubReleaseDir -Force
if (Test-Path ".\releases-x64\ShipvillanWinSetup.exe") {
    Copy-Item ".\releases-x64\ShipvillanWinSetup.exe" "$GithubReleaseDir\Setup-x64.exe" -Force
}

# Copy x64 RELEASES file as the main RELEASES file (for auto-updates)
Write-Host "Setting up auto-update (x64 only)..." -ForegroundColor Yellow
Copy-Item ".\releases-x64\RELEASES" "$GithubReleaseDir\RELEASES" -Force

Write-Host ""
Write-Host "=====================================" -ForegroundColor Green
Write-Host "   Build Complete!" -ForegroundColor Green
Write-Host "=====================================" -ForegroundColor Green
Write-Host ""
Write-Host "GitHub Release folder ready:" -ForegroundColor Cyan
Write-Host "  Location: .\github-release\" -ForegroundColor White
Write-Host ""
Write-Host "Files to upload to GitHub release v$Version" -ForegroundColor Yellow
Get-ChildItem $GithubReleaseDir | ForEach-Object {
    Write-Host "  - $($_.Name)" -ForegroundColor White
}
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Cyan
Write-Host "1. Create GitHub release: v$Version" -ForegroundColor White
Write-Host "2. Upload ALL files from .\github-release\ folder" -ForegroundColor White
Write-Host "3. Publish the release (not draft)" -ForegroundColor White
Write-Host ""
Write-Host "Auto-update configuration:" -ForegroundColor Yellow
Write-Host "  - x64 systems: Auto-updates ENABLED (via RELEASES file)" -ForegroundColor Green
Write-Host "  - x86 systems: Manual install only (no auto-update)" -ForegroundColor Yellow
Write-Host ""
