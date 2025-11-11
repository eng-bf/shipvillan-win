# ShipvillanWin

A Windows system tray application for warehouse barcode scanning with automatic updates.

## What It Does

ShipvillanWin runs in the background and processes barcodes from USB/COM port scanners in three modes:

- **Order Assignment (MX)** - Forwards all barcodes immediately while performing async order-to-tote pairing in background
- **Interception (US)** - Processes CT- prefixed barcodes with async transformations before forwarding
- **Passthrough** - Forwards all barcodes immediately without any processing (useful for testing/troubleshooting)

**Key Features:**
- Automatic updates via GitHub Releases (checks daily after 3pm PST)
- Auto-starts at Windows login
- COM port auto-detection
- Manual update check & rollback options in tray menu

---

## Quick Install (End Users)

```powershell
irm https://raw.githubusercontent.com/eng-bf/shipvillan-win/main/install.ps1 | iex
```

This installs the app, sets up auto-start, and enables automatic updates.

---

## Development Setup

### Prerequisites
- Windows 10/11 (build 17763+)
- [.NET SDK 8.x](https://dotnet.microsoft.com/download/dotnet/8.0)

### Run Locally

```bash
cd src\ShipvillanWin
dotnet run
```

The app appears in the system tray. Right-click to configure mode and COM port.

### Debug Output

To see debug logs, use [DebugView](https://learn.microsoft.com/sysinternals/downloads/debugview):
1. Run DebugView as Administrator
2. Enable **Capture → Capture Global Win32**
3. Run `dotnet run`
4. See all Debug.WriteLine output in DebugView

---

## Publishing a New Release

ShipvillanWin supports both **x86 (32-bit)** and **x64 (64-bit)** architectures. The build process creates separate packages for each architecture, and the installer automatically detects and installs the correct version.

### 1. Update Version

Edit `src/ShipvillanWin/ShipvillanWin.csproj`:
```xml
<Version>1.0.1</Version>          <!-- Increment version -->
<AssemblyVersion>1.0.1</AssemblyVersion>
<FileVersion>1.0.1</FileVersion>
```

**Version format:** `MAJOR.MINOR.PATCH`
- **PATCH** (1.0.0 → 1.0.1) - Bug fixes
- **MINOR** (1.0.0 → 1.1.0) - New features
- **MAJOR** (1.0.0 → 2.0.0) - Breaking changes

### 2. Download Squirrel Tools (First time only)

```bash
.\download-squirrel.ps1
```

This downloads all packaging tools to the `tools/` folder.

### 3. Build Release Packages (Both Architectures)

Use the automated build script:

```bash
.\build-release.ps1 -Version 1.0.1
```

This script will:
- Build both x86 and x64 versions
- Package them with Squirrel
- Create organized release folders

**Output:**
- `releases-x86/Setup-x86.exe` - x86 installer
- `releases-x86/ShipvillanWin-x86-1.0.1-full.nupkg` - x86 update package
- `releases-x86/RELEASES-x86` - x86 metadata file
- `releases-x64/Setup-x64.exe` - x64 installer
- `releases-x64/ShipvillanWin-x64-1.0.1-full.nupkg` - x64 update package
- `releases-x64/RELEASES-x64` - x64 metadata file

### 4. Create GitHub Release

1. Go to: https://github.com/eng-bf/shipvillan-win/releases/new
2. Tag: `v1.0.1` (must match version)
3. Title: `ShipvillanWin v1.0.1`
4. Attach **ALL** files from both architectures:
   - `Setup-x86.exe`
   - `ShipvillanWin-x86-1.0.1-full.nupkg`
   - `RELEASES-x86`
   - `Setup-x64.exe`
   - `ShipvillanWin-x64-1.0.1-full.nupkg`
   - `RELEASES-x64`
5. **IMPORTANT**: Also upload a copy of `RELEASES-x64` renamed to just `RELEASES` (this enables auto-updates for x64 systems)
6. Click **Publish Release**

**Done!** The installer will automatically detect the system architecture and install the correct version. Warehouse machines will auto-update after 3pm PST (or manually via tray menu).

#### Auto-Update Notes

- **x64 systems**: Will auto-update correctly using the `RELEASES` file
- **x86 systems**: Currently require manual reinstall for architecture changes (auto-updates work within the same architecture)
- **Future improvement**: Full architecture-aware auto-updating can be implemented in UpdateService.cs

---

## Optional: Add Custom Tray Icon

1. Create `src/ShipvillanWin/Resources/tray-icon.ico` (16x16 icon required)
2. Edit `src/ShipvillanWin/ShipvillanWin.csproj`, add before `</Project>`:
   ```xml
   <ItemGroup>
     <EmbeddedResource Include="Resources\tray-icon.ico">
       <LogicalName>ShipvillanWin.Resources.tray-icon.ico</LogicalName>
     </EmbeddedResource>
   </ItemGroup>
   ```
3. Rebuild - custom icon appears in tray

---

## Project Structure

```
src/ShipvillanWin/
├── Program.cs                      # Entry point
├── TrayAppContext.cs               # Tray menu & mode switching
├── UpdateService.cs                # Auto-update logic
├── ComPortManager.cs               # Scanner connection
├── BarcodeProcessor.cs             # Interception mode
├── OrderAssignmentProcessor.cs     # Order Assignment mode
├── InterceptionService.cs          # CT- barcode transformation
├── OrderAssignmentService.cs       # Tote/order pairing API
└── KeyboardSimulator.cs            # Send barcodes as keystrokes

tools/                              # Squirrel packaging tools (gitignored)
releases/                           # Built installers (gitignored)
install.ps1                         # One-line installer script
download-squirrel.ps1               # Download packaging tools
```

---

## Configuration

Settings stored in: `%APPDATA%\ShipvillanWin\config.json`

```json
{
  "Mode": "Interception",
  "ComPort": "COM3",
  "BaudRate": 9600,
  "DataBits": 8,
  "BarcodePrefix": "CT-",
  "KeyboardDelayMs": 10,
  "AppendEnterKey": true,
  "InterceptionTimeoutMs": 5000
}
```

Changes via tray menu auto-save to this file.

---

## Troubleshooting

**Squirrel tools not found**
```bash
.\download-squirrel.ps1
```

**Updates not detected**
- Verify version was incremented in `.csproj`
- Check `RELEASES` file uploaded to GitHub
- Ensure release is published (not draft)

**COM port not detected**
- Check scanner is USB/Serial (not HID keyboard mode)
- Try different COM port in tray menu
- Verify baud rate matches scanner (default: 9600)

---

## Uninstall

### Complete Removal (Clean Slate)

To completely remove ShipvillanWin from a machine:

**1. Uninstall the application**
```powershell
# Stop the app if running
Stop-Process -Name ShipvillanWin -Force -ErrorAction SilentlyContinue

# Uninstall via Windows Settings
explorer.exe ms-settings:appsfeatures
# Search for "ShipvillanWin" and click Uninstall
```

**OR use Squirrel uninstaller directly:**
```powershell
& "$env:LOCALAPPDATA\ShipvillanWin\Update.exe" --uninstall
```

**2. Remove all configuration and data**
```powershell
# Remove app data folder
Remove-Item -Path "$env:APPDATA\ShipvillanWin" -Recurse -Force -ErrorAction SilentlyContinue

# Remove installation folder
Remove-Item -Path "$env:LOCALAPPDATA\ShipvillanWin" -Recurse -Force -ErrorAction SilentlyContinue

# Remove auto-start registry entry (if not cleaned by uninstaller)
Remove-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" -Name "ShipvillanWin" -ErrorAction SilentlyContinue
```

**3. Verify clean removal**
```powershell
# Check registry
Get-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" -Name "ShipvillanWin" -ErrorAction SilentlyContinue

# Check folders
Test-Path "$env:APPDATA\ShipvillanWin"
Test-Path "$env:LOCALAPPDATA\ShipvillanWin"
```

All commands should return nothing/false if removal is complete.

---

## Notes

- **Dual architecture support** - Automatically detects and installs x86 or x64 version
- **Repository is private** but releases are public (enables auto-updates without auth)
- **Auto-updates check daily** but only install after 3pm PST
- **Architecture-aware updates** - x86 installs stay on x86, x64 installs stay on x64
- **IT can force update** anytime via tray menu "Check for Updates"
- **Rollback available** via tray menu "Rollback to Previous Version"
- **Three operation modes** can be switched via tray menu (requires app restart)
