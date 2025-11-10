#requires -Version 5.1
<#
.SYNOPSIS
  Installs HelloTrayApp (x86) for current user.

.DESCRIPTION
  Downloads a published ZIP from your CDN, extracts to %LOCALAPPDATA%\HelloTrayApp,
  registers auto-start (HKCU\...\Run), and launches the app.

  Usage:
    irm https://YOUR-CDN/hello-tray-app/install.ps1 | iex
    # optionally specify a version or custom URI:
    irm https://YOUR-CDN/hello-tray-app/install.ps1 | iex -Version "1.0.0" -BaseUri "https://cdn.example.com/hello-tray-app/releases"

.PARAMETER Version
  Release tag/folder name on your CDN. Default: "latest"

.PARAMETER BaseUri
  Base CDN URI that hosts ZIP artifacts. No trailing slash.
#>

param(
  [string]$Version = "latest",
  [string]$BaseUri = "https://cdn.example.com/hello-tray-app/releases"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# Ensure TLS 1.2+
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$AppName = "HelloTrayApp"
$InstallRoot = Join-Path $env:LOCALAPPDATA $AppName
$ZipName = "$AppName-win-x86.zip"
$ZipUrl  = "$BaseUri/$Version/$ZipName"
$TempZip = Join-Path $env:TEMP "$AppName-$Version.zip"
$ExePath = Join-Path $InstallRoot "HelloTrayApp.exe"
$RunKey  = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"

Write-Host "Installing $AppName ($Version)..." 

# Create install directory
if (-not (Test-Path $InstallRoot)) { New-Item -ItemType Directory -Path $InstallRoot | Out-Null }

# Download zip
Write-Host "Downloading: $ZipUrl"
Invoke-WebRequest -UseBasicParsing -Uri $ZipUrl -OutFile $TempZip

# Stop running app if present
$existing = Get-Process HelloTrayApp -ErrorAction SilentlyContinue
if ($existing) {
  Write-Host "Stopping existing $AppName..."
  $existing | Stop-Process -Force
  Start-Sleep -Milliseconds 300
}

# Unzip
Write-Host "Extracting to $InstallRoot"
if (Test-Path $InstallRoot) {
  Get-ChildItem $InstallRoot -Recurse -Force | Remove-Item -Force -Recurse -ErrorAction SilentlyContinue
}
Expand-Archive -Path $TempZip -DestinationPath $InstallRoot -Force

# Register auto-start
Write-Host "Registering auto-start..."
$quoted = '"' + $ExePath + '"'
New-ItemProperty -Path $RunKey -Name $AppName -Value $quoted -PropertyType String -Force | Out-Null

# Launch
Write-Host "Launching $AppName..."
Start-Process -FilePath $ExePath

# Cleanup
Remove-Item $TempZip -Force -ErrorAction SilentlyContinue

Write-Host "$AppName installed to $InstallRoot"
Write-Host "You should now see the tray icon. Right-click it for options."

# Optional: Uninstall function users can call later in session
function Uninstall-HelloTrayApp {
  Write-Host "Uninstalling $AppName..."
  $p = Get-Process HelloTrayApp -ErrorAction SilentlyContinue
  if ($p) { $p | Stop-Process -Force }
  Remove-ItemProperty -Path $RunKey -Name $AppName -ErrorAction SilentlyContinue
  if (Test-Path $InstallRoot) { Remove-Item $InstallRoot -Recurse -Force }
  Write-Host "$AppName uninstalled."
}
