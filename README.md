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

### 3. Build Release Package

```bash
# Build the application
dotnet publish src\ShipvillanWin\ShipvillanWin.csproj -c Release -r win-x86 --self-contained false -o .\publish

# Create installer package
.\tools\squirrel.exe pack --packId ShipvillanWin --packVersion 1.0.1 --packDirectory .\publish --releaseDir .\releases --allowUnaware
```

This creates:
- `releases/ShipvillanWinSetup.exe` - Installer
- `releases/ShipvillanWin-1.0.1-full.nupkg` - Update package
- `releases/RELEASES` - Metadata file

### 4. Create GitHub Release

1. Go to: https://github.com/eng-bf/shipvillan-win/releases/new
2. Tag: `v1.0.1` (must match version)
3. Title: `ShipvillanWin v1.0.1`
4. Attach files:
   - `ShipvillanWinSetup.exe` (rename to `Setup.exe`)
   - `ShipvillanWin-1.0.1-full.nupkg`
   - `RELEASES`
5. Click **Publish Release**

**Done!** Warehouse machines will auto-update after 3pm PST (or manually via tray menu).

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

## Notes

- **Repository is private** but releases are public (enables auto-updates without auth)
- **Auto-updates check daily** but only install after 3pm PST
- **IT can force update** anytime via tray menu "Check for Updates"
- **Rollback available** via tray menu "Rollback to Previous Version"
- **Three operation modes** can be switched via tray menu (requires app restart)
