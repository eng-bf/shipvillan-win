# ShipvillanWin Dual-Architecture Build Script
# Builds and packages both x86 and x64 versions
#
# Usage: .\build-release.ps1 -Version 1.0.1

param(
    [Parameter(Mandatory=$true)]
    [string]$Version
)

$ErrorActionPreference = "Stop"

$ProjectPath = "src\ShipvillanWin\ShipvillanWin.csproj"
$SquirrelExe = ".\tools\squirrel.exe"

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

# Package x64 with Squirrel
Write-Host "Packaging x64 with Squirrel..." -ForegroundColor Yellow
& $SquirrelExe pack `
    --packId ShipvillanWin-x64 `
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

# Rename Setup.exe files for clarity
Write-Host "Renaming installers for clarity..." -ForegroundColor Yellow
if (Test-Path ".\releases-x86\ShipvillanWin-x86Setup.exe") {
    Rename-Item ".\releases-x86\ShipvillanWin-x86Setup.exe" "Setup-x86.exe"
}
if (Test-Path ".\releases-x64\ShipvillanWin-x64Setup.exe") {
    Rename-Item ".\releases-x64\ShipvillanWin-x64Setup.exe" "Setup-x64.exe"
}

# Rename RELEASES files for architecture-specific updates
Write-Host "Renaming RELEASES files..." -ForegroundColor Yellow
if (Test-Path ".\releases-x86\RELEASES") {
    Copy-Item ".\releases-x86\RELEASES" ".\releases-x86\RELEASES-x86"
}
if (Test-Path ".\releases-x64\RELEASES") {
    Copy-Item ".\releases-x64\RELEASES" ".\releases-x64\RELEASES-x64"
}

Write-Host ""
Write-Host "=====================================" -ForegroundColor Green
Write-Host "   Build Complete!" -ForegroundColor Green
Write-Host "=====================================" -ForegroundColor Green
Write-Host ""
Write-Host "Release files created:" -ForegroundColor Cyan
Write-Host ""
Write-Host "x86 (32-bit):" -ForegroundColor Yellow
Write-Host "  .\releases-x86\Setup-x86.exe"
Write-Host "  .\releases-x86\ShipvillanWin-x86-$Version-full.nupkg"
Write-Host "  .\releases-x86\RELEASES-x86"
Write-Host ""
Write-Host "x64 (64-bit):" -ForegroundColor Yellow
Write-Host "  .\releases-x64\Setup-x64.exe"
Write-Host "  .\releases-x64\ShipvillanWin-x64-$Version-full.nupkg"
Write-Host "  .\releases-x64\RELEASES-x64"
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Cyan
Write-Host "1. Create GitHub release: v$Version"
Write-Host "2. Upload ALL files from both releases-x86 and releases-x64 folders"
Write-Host "3. IMPORTANT: Copy RELEASES-x64 to RELEASES (for auto-updater compatibility)"
Write-Host "4. Ensure release is published (not draft)"
Write-Host ""
Write-Host "Note: The auto-updater currently uses the RELEASES file (not architecture-specific)." -ForegroundColor Yellow
Write-Host "Copying RELEASES-x64 to RELEASES ensures x64 systems get x64 updates." -ForegroundColor Yellow
Write-Host "x86 systems will need to manually reinstall when upgrading." -ForegroundColor Yellow
Write-Host ""
