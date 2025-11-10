# Download Squirrel.Windows tools
$ErrorActionPreference = "Stop"

$version = "2.11.1"
$nugetUrl = "https://www.nuget.org/api/v2/package/Clowd.Squirrel/$version"
$toolsDir = "$PSScriptRoot\tools\squirrel"

Write-Host "Downloading Squirrel.Windows v$version..." -ForegroundColor Yellow

# Create tools directory
New-Item -ItemType Directory -Path $toolsDir -Force | Out-Null

# Download NuGet package
$zipPath = "$toolsDir\squirrel.zip"
Invoke-WebRequest -Uri $nugetUrl -OutFile $zipPath -UseBasicParsing

# Extract
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::ExtractToDirectory($zipPath, $toolsDir)

# Find and copy squirrel.exe
$squirrelExe = Get-ChildItem -Path $toolsDir -Recurse -Filter "squirrel.exe" | Select-Object -First 1

if ($squirrelExe) {
    Copy-Item $squirrelExe.FullName -Destination "$PSScriptRoot\squirrel.exe" -Force
    Write-Host "Squirrel.exe downloaded to: $PSScriptRoot\squirrel.exe" -ForegroundColor Green

    # Cleanup
    Remove-Item $toolsDir -Recurse -Force

    Write-Host ""
    Write-Host "To create a release, use:" -ForegroundColor Cyan
    Write-Host ".\squirrel.exe pack --packId ShipvillanWin --packVersion 1.0.0 --packDirectory .\publish --releaseDir .\releases" -ForegroundColor White
} else {
    Write-Error "Could not find squirrel.exe in package"
}
