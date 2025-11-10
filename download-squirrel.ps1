# Download Squirrel.Windows tools
$ErrorActionPreference = "Stop"

$version = "2.11.1"
$nugetUrl = "https://www.nuget.org/api/v2/package/Clowd.Squirrel/$version"
$tempDir = "$PSScriptRoot\tools\squirrel-temp"
$toolsDir = "$PSScriptRoot\tools"

Write-Host "Downloading Squirrel.Windows v$version..." -ForegroundColor Yellow

# Create temp directory
New-Item -ItemType Directory -Path $tempDir -Force | Out-Null

# Download NuGet package
$zipPath = "$tempDir\squirrel.zip"
Write-Host "Downloading from NuGet..." -ForegroundColor Yellow
Invoke-WebRequest -Uri $nugetUrl -OutFile $zipPath -UseBasicParsing

# Extract
Write-Host "Extracting package..." -ForegroundColor Yellow
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::ExtractToDirectory($zipPath, $tempDir)

# Find the tools folder inside the NuGet package
$nugetToolsFolder = Get-ChildItem -Path $tempDir -Directory -Filter "tools" -Recurse | Select-Object -First 1

if ($nugetToolsFolder) {
    # Copy all tools to repository tools folder
    Write-Host "Copying tools..." -ForegroundColor Yellow
    Copy-Item -Path "$($nugetToolsFolder.FullName)\*" -Destination $toolsDir -Recurse -Force

    Write-Host ""
    Write-Host "Squirrel tools installed successfully!" -ForegroundColor Green
    Write-Host ""

    # List what was installed
    $installedFiles = Get-ChildItem -Path $toolsDir -File | Select-Object -ExpandProperty Name
    Write-Host "Installed files:" -ForegroundColor Cyan
    $installedFiles | ForEach-Object { Write-Host "  - $_" -ForegroundColor White }

    Write-Host ""
    Write-Host "To create a release, use:" -ForegroundColor Cyan
    Write-Host "  .\tools\squirrel.exe pack --packId ShipvillanWin --packVersion 1.0.0 --packDirectory .\publish --releaseDir .\releases" -ForegroundColor White
} else {
    Write-Error "Could not find tools folder in package"
}

# Cleanup temp directory
Write-Host ""
Write-Host "Cleaning up..." -ForegroundColor Yellow
Remove-Item $tempDir -Recurse -Force

Write-Host "Done!" -ForegroundColor Green
