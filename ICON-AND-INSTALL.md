# Custom Icon & PowerShell Installation Guide

## Adding a Custom Tray Icon

The application now supports custom tray icons while maintaining single-file deployment. The icon is embedded as a resource in the executable.

### Step 1: Create or Obtain an Icon File

Create a `.ico` file (Windows icon format) with multiple sizes:
- 16x16 (required for tray icon)
- 32x32 (recommended)
- 48x48 (recommended)
- 256x256 (optional, for high DPI)

**Tools to create icons:**
- **GIMP** (Free) - Export as .ico
- **Paint.NET** + Icon plugin (Free)
- **IcoFX** (Paid)
- **Online converters** - Convert PNG to ICO

### Step 2: Add Icon to Project

1. Create a `Resources` folder:
   ```
   src/ShipvillanWin/Resources/
   ```

2. Place your icon file:
   ```
   src/ShipvillanWin/Resources/tray-icon.ico
   ```

3. Edit `ShipvillanWin.csproj` to embed the icon:

   ```xml
   <ItemGroup>
     <EmbeddedResource Include="Resources\tray-icon.ico">
       <LogicalName>ShipvillanWin.Resources.tray-icon.ico</LogicalName>
     </EmbeddedResource>
   </ItemGroup>
   ```

   Add this **before** the closing `</Project>` tag.

### Step 3: Build and Test

```bash
dotnet build
dotnet run
```

The custom icon should now appear in the system tray!

**If the icon doesn't load:**
- Check Debug output for error messages
- Verify the icon file exists at `Resources/tray-icon.ico`
- Ensure the `LogicalName` matches: `ShipvillanWin.Resources.tray-icon.ico`
- The app will automatically fall back to the system icon if loading fails

### Optional: Set Application Icon (Alt+Tab, Taskbar)

To also set the application icon (shown in Alt+Tab switcher, taskbar when visible):

Edit `ShipvillanWin.csproj`:

```xml
<PropertyGroup>
  <ApplicationIcon>Resources\tray-icon.ico</ApplicationIcon>
</PropertyGroup>
```

This sets the main executable icon. The tray icon is handled separately via `IconHelper.cs`.

---

## PowerShell Installation Script

The repository includes `install.ps1` for easy one-line installation from GitHub releases.

### Installation Methods

#### Method 1: Remote Install (Recommended for End Users)

```powershell
irm https://raw.githubusercontent.com/eng-bf/shipvillan-win/main/install.ps1 | iex
```

This:
- Downloads the latest release from GitHub
- Runs the Squirrel installer silently
- Sets up auto-start
- Starts the application

#### Method 2: Local Install

```powershell
# Download the script
Invoke-WebRequest -Uri "https://raw.githubusercontent.com/eng-bf/shipvillan-win/main/install.ps1" -OutFile install.ps1

# Run it
.\install.ps1
```

#### Method 3: Force Reinstall (Stops Running App)

```powershell
irm https://raw.githubusercontent.com/eng-bf/shipvillan-win/main/install.ps1 | iex -Force
```

### What the Installer Does

1. ✅ Checks if app is already running (stops if `-Force` is used)
2. ✅ Fetches latest release info from GitHub API
3. ✅ Downloads `Setup.exe` from latest release
4. ✅ Runs installer silently
5. ✅ Cleans up temporary files
6. ✅ Reports success/failure

### Installation Locations

**Squirrel installs to:**
```
%LOCALAPPDATA%\ShipvillanWin\
├── app-1.0.0\           (Current version)
├── packages\            (Update packages)
├── ShipvillanWin.exe    (Launch shortcut)
└── Update.exe           (Squirrel updater)
```

**Shortcuts created:**
- Desktop: `%USERPROFILE%\Desktop\ShipvillanWin.lnk`
- Start Menu: `%APPDATA%\Microsoft\Windows\Start Menu\Programs\ShipvillanWin.lnk`

**Auto-start registry:**
```
HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run
Name: ShipvillanWin
```

### Script Compatibility with Current Setup

✅ **YES**, the install script is fully compatible with:
- Squirrel auto-updates
- Private GitHub repository with public releases
- All three operation modes (Order Assignment, Interception, Passthrough)
- Custom embedded icons
- Auto-start functionality
- COM port configuration

The installer uses GitHub Releases API which works with public release assets even from private repositories.

### Customizing the Install Script

**Change installation method:**
```powershell
# Use specific version
.\install.ps1 -Version "v1.0.5"

# Force reinstall (stops app if running)
.\install.ps1 -Force
```

**For IT departments:**
- Script can be modified to deploy from internal network share
- Can be integrated into SCCM, Intune, or other deployment tools
- Silent installation works without user interaction

### Hosting the Install Script

For the `irm https://... | iex` pattern to work, `install.ps1` must be:

1. **In your repository root** (already done)
2. **Accessible via raw URL:**
   ```
   https://raw.githubusercontent.com/eng-bf/shipvillan-win/main/install.ps1
   ```

**To use custom domain:**
```powershell
# Host on your own server
irm https://install.bajafulfillment.com/shipvillan.ps1 | iex

# Or use URL shortener
irm https://bit.ly/install-shipvillan | iex
```

### Testing the Installer

**Local test before pushing to GitHub:**

1. Start a local HTTP server in repository root:
   ```powershell
   python -m http.server 8080
   ```

2. Test install script:
   ```powershell
   # Modify install.ps1 temporarily to use localhost
   $GitHubRepo = "localhost:8080"
   ```

3. Once verified, push to GitHub

**Test from GitHub:**
```powershell
# After pushing install.ps1 to main branch
irm https://raw.githubusercontent.com/eng-bf/shipvillan-win/main/install.ps1 | iex
```

### Uninstallation

The Squirrel installer creates an uninstaller:

```powershell
# Via Windows Settings
start ms-settings:appsfeatures

# Or directly
%LOCALAPPDATA%\ShipvillanWin\Update.exe --uninstall
```

---

## Complete Deployment Workflow

### For Developers:

1. Make changes to code
2. Update version in `ShipvillanWin.csproj`
3. Build and create Squirrel package
4. Create GitHub release with all assets
5. Push `install.ps1` to repository (one-time setup)

### For End Users:

```powershell
# One-line install
irm https://raw.githubusercontent.com/eng-bf/shipvillan-win/main/install.ps1 | iex
```

That's it! The app:
- ✅ Installs silently
- ✅ Starts automatically
- ✅ Auto-updates daily (after 3pm PST)
- ✅ Shows in system tray with custom icon

### For IT Departments:

```powershell
# Deploy via GPO, SCCM, or script
Invoke-Expression (Invoke-WebRequest -Uri "https://raw.githubusercontent.com/eng-bf/shipvillan-win/main/install.ps1" -UseBasicParsing).Content
```

Or download `install.ps1` and `Setup.exe` to network share for offline installation.

---

## Troubleshooting

### Icon doesn't appear
- Verify `Resources/tray-icon.ico` exists
- Check that `EmbeddedResource` is added to `.csproj`
- Rebuild the project
- Check Debug output for loading errors

### Install script fails
- Ensure release has `Setup.exe` asset
- Check that RELEASES file is included in GitHub release
- Verify internet connectivity
- Check Windows Event Viewer for Squirrel errors

### App doesn't auto-start
- Check registry: `HKCU\...\Run`
- Verify Squirrel shortcuts were created
- Run as administrator if needed

### Can't install while app is running
- Use `-Force` parameter
- Or manually stop app from tray icon
- Or kill process: `Stop-Process -Name ShipvillanWin -Force`
