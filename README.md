# ShipvillanWin (x86, .NET 8)

A Windows system tray application for warehouse barcode processing with multiple operation modes:
- **Order Assignment (MX)** - Always forwards barcodes, performs async order-to-tote pairing
- **Interception (US)** - Processes CT- prefixed barcodes with async transformations
- **Passthrough** - Disables all processing, forwards barcodes immediately
- Auto-updates via GitHub Releases
- Runs in the background with system tray icon
- Starts automatically at user login
- Targets **x86** (32-bit) Windows

## Quick Install (End Users)

Install with one PowerShell command:

```powershell
irm https://raw.githubusercontent.com/eng-bf/shipvillan-win/main/install.ps1 | iex
```

This will:
- ✅ Download and install the latest version
- ✅ Set up auto-start at login
- ✅ Enable automatic updates
- ✅ Start the application in the system tray

Right-click the tray icon to configure operation mode and COM port settings.

## Prerequisites (Developers)

- Windows 10/11 (minimum version: 1809 / build 17763)
- [.NET SDK 8.x](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)
- (Dev) Visual Studio Code or Visual Studio

## Run in Dev

```powershell
cd src\ShipvillanWin
dotnet run
```

## Build (Debug/Release)

```powershell
cd src\ShipvillanWin
```

### Debug
```powershell
dotnet build
```

### Release (x86 enforced by csproj)
```powershell
dotnet build -c Release
```

## Publish (Self-contained, Single File, x86)

Produces a single EXE you can zip and host on a CDN:

```powershell
cd src\ShipvillanWin

dotnet publish -c Release -r win-x86 ^
  -p:SelfContained=true ^
  -p:PublishSingleFile=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true ^
  -o ..\..\publish\win-x86
```

Output directory: `publish\win-x86`. Zip its contents as `ShipvillanWin-win-x86.zip` for distribution.

## Auto-Start Behavior

On first run, the app writes a value under:

```
HKCU\Software\Microsoft\Windows\CurrentVersion\Run
Name: ShipvillanWin
Data: "C:\Path\To\ShipvillanWin.exe"
```

The application automatically configures itself to start when the user logs in. This can be managed programmatically via the `AutoStart` class.

## Development Workflow

### Testing Code Changes

When you edit the code, run this command to test your latest changes:

```powershell
cd src\ShipvillanWin
dotnet run
```

`dotnet run` automatically detects source file changes, rebuilds if needed, and runs the updated application.

### Common Commands

**Quick Testing (Most Common)**
```powershell
dotnet run
```
- Fastest for development
- Auto-rebuilds on changes
- Framework-dependent (uses installed runtime)

**Manual Build + Run**
```powershell
dotnet build        # Check for compile errors
dotnet run          # Then run
```

**Testing Release Build**
```powershell
dotnet build -c Release
# Then run: bin\Release\net8.0-windows10.0.17763.0\ShipvillanWin.exe
```

**Creating Distribution Package**
```powershell
dotnet publish -c Release -r win-x86 ^
  -p:SelfContained=true ^
  -p:PublishSingleFile=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true ^
  -o ..\..\publish\win-x86
```
- Run this only when ready to distribute the 168MB self-contained executable
- This is for distributing to end users who may not have .NET installed

### Recommended Development Flow

1. **Edit code** in your editor
2. **Test**: `dotnet run`
3. **Repeat** steps 1-2 as needed
4. **Publish** only when ready to distribute

### Build Types Comparison

| Build Type | Size | Needs Runtime? | Use Case |
|------------|------|----------------|----------|
| Debug (dotnet run) | ~120KB | Yes (dev machine has it) | Development & testing |
| Release Self-Contained | 168MB | No | End-user distribution |

## Complete Update Flow: From Code to Warehouse Deployment

This application uses **automatic updates** via GitHub Releases. Here's the complete workflow from making changes to deploying to all warehouse machines.

### Prerequisites for Releases

1. Download Squirrel.Windows tools:
   ```bash
   # Run this once to download squirrel.exe
   .\download-squirrel.ps1
   ```
   This downloads `squirrel.exe` to the repository root.

2. Ensure you have push access to the repository

### Step-by-Step Release Process

#### 1. Create a Feature Branch

```bash
# Make sure you're on main and up to date
git checkout main
git pull origin main

# Create a new branch for your changes
git checkout -b feature/add-new-functionality
# or: git checkout -b bugfix/fix-scanner-issue
```

#### 2. Make Your Changes

Edit the code as needed, then test locally:

```bash
cd src\ShipvillanWin
dotnet run
```

Test thoroughly to ensure:
- No compilation errors
- Feature works as expected
- No regressions in existing functionality

#### 3. Update Version Number

Before committing, update the version in `src\ShipvillanWin\ShipvillanWin.csproj`:

```xml
<Version>1.0.1</Version>          <!-- Change from 1.0.0 to 1.0.1 -->
<AssemblyVersion>1.0.1</AssemblyVersion>
<FileVersion>1.0.1</FileVersion>
```

**Version numbering (Semantic Versioning):**
- `1.0.0` → `1.0.1` - Bug fixes (PATCH)
- `1.0.0` → `1.1.0` - New features (MINOR)
- `1.0.0` → `2.0.0` - Breaking changes (MAJOR)

#### 4. Commit and Push

```bash
# Stage all changes
git add .

# Commit with descriptive message
git commit -m "Add barcode filtering for Canadian shipments"

# Push to GitHub
git push origin feature/add-new-functionality
```

#### 5. Create Pull Request (PR)

1. Go to: https://github.com/eng-bf/shipvillan-win/pulls
2. Click **"New Pull Request"**
3. Base: `main` ← Compare: `feature/add-new-functionality`
4. Fill in PR details:
   - **Title**: "Add barcode filtering for Canadian shipments"
   - **Description**: Explain what changed and why
   - List any testing performed
5. Click **"Create Pull Request"**

#### 6. Review and Merge PR

1. Have another developer review (if applicable)
2. Address any feedback
3. Once approved, click **"Merge Pull Request"**
4. Click **"Confirm Merge"**
5. Optionally delete the feature branch

#### 7. Build Release Package

```bash
# Switch to main branch
git checkout main
git pull origin main

# Navigate to project root
cd C:\Users\gwin\src\shipvillan-win

# Build the application for release
dotnet publish src\ShipvillanWin\ShipvillanWin.csproj -c Release -r win-x86 --self-contained false -o .\publish

# Create Squirrel release package
.\squirrel.exe pack --packId ShipvillanWin --packVersion 1.0.1 --packDirectory .\publish --releaseDir .\releases
```

This creates several files in the `releases` folder:
- `Setup.exe` - Installer for new installations
- `ShipvillanWin-1.0.1-full.nupkg` - Full update package
- `ShipvillanWin-1.0.1-delta.nupkg` - Delta update (only changes)
- `RELEASES` - Metadata file

#### 8. Create GitHub Release

1. Go to: https://github.com/eng-bf/shipvillan-win/releases/new
2. Fill in release details:
   - **Tag**: `v1.0.1` (must match version number)
   - **Target**: `main`
   - **Title**: `ShipvillanWin v1.0.1`
   - **Description**:
     ```markdown
     ## What's New
     - Added barcode filtering for Canadian shipments
     - Fixed scanner timeout issue

     ## Bug Fixes
     - Resolved crash when COM port is disconnected

     ## Installation
     - New installs: Download and run `Setup.exe`
     - Existing users: Automatic update will be applied after 3pm PST
     ```
3. **Attach files** (drag and drop into the release):
   - `Setup.exe`
   - `ShipvillanWin-1.0.1-full.nupkg`
   - `ShipvillanWin-1.0.1-delta.nupkg` (if exists)
   - `RELEASES`
4. **IMPORTANT**: Leave "Set as a pre-release" unchecked
5. Click **"Publish Release"**

**Note**: The repository is private, but release assets are **publicly accessible** for download. This allows warehouse machines to update without authentication.

#### 9. Automatic Update to Warehouses

Once published, warehouse machines will automatically update:

**Automatic Updates:**
- Every hour, the app checks for new releases
- If a new version is found AND it's after 3pm PST, the update downloads
- Update installs silently in the background
- Application automatically restarts
- Users see a notification: "Update downloaded. Restarting application..."

**Manual Updates (IT Staff):**
- Right-click the tray icon
- Select "Check for Updates"
- Update happens immediately (bypasses 3pm restriction)

#### 10. Monitor Deployment

After publishing a release:

1. **Verify release is accessible**:
   - Go to: https://github.com/eng-bf/shipvillan-win/releases/latest
   - Ensure all files are attached and downloadable

2. **Test on one machine first** (if critical update):
   - Connect to a test warehouse machine
   - Trigger manual update: Right-click tray → "Check for Updates"
   - Verify update completes successfully
   - Test functionality

3. **Monitor rollout**:
   - First warehouse will update after 3pm PST (or manual trigger)
   - Over next 24 hours, all warehouses update
   - Check for error reports from warehouse staff

### Update Rollback

If a bad release is deployed:

**Option 1: Have users rollback manually**
1. Users right-click tray icon
2. Select "Rollback to Previous Version"
3. Confirm the rollback
4. Application restarts with previous version

**Option 2: Deploy hotfix (recommended)**
1. Fix the bug quickly
2. Increment version to 1.0.2
3. Follow steps 1-8 above
4. Push hotfix release immediately

**Option 3: Delete bad release**
1. Go to: https://github.com/eng-bf/shipvillan-win/releases
2. Find the bad release
3. Click "Delete" (machines won't update to it)
4. Deploy proper fix as new release

### Testing Updates Before Production

To test the update mechanism without deploying to production:

```bash
# Create a test release with a higher version
.\squirrel.exe pack --packId ShipvillanWin --packVersion 1.0.2-beta --packDirectory .\publish --releaseDir .\test-releases

# Start a local HTTP server
cd test-releases
python -m http.server 8080
```

Temporarily modify `TrayAppContext.cs`:
```csharp
_updateService = new UpdateService("http://localhost:8080");
```

Test the update, then revert the change before committing.

### Quick Reference

**Common Git Commands:**
```bash
git status                                    # Check what's changed
git checkout -b feature/my-feature           # Create new branch
git add .                                     # Stage all changes
git commit -m "description"                   # Commit changes
git push origin feature/my-feature           # Push to GitHub
git checkout main                             # Switch to main
git pull origin main                          # Get latest from main
```

**Version Update Locations:**
- `src\ShipvillanWin\ShipvillanWin.csproj` - Lines 18-20

**Build Commands:**
```bash
# Test locally
dotnet run

# Build release
dotnet publish src\ShipvillanWin\ShipvillanWin.csproj -c Release -r win-x86 --self-contained false -o .\publish

# Create update package
.\squirrel.exe pack --packId ShipvillanWin --packVersion X.Y.Z --packDirectory .\publish --releaseDir .\releases
```

### Troubleshooting

**"Updates not detected"**
- Verify version number in `.csproj` was incremented
- Check that `RELEASES` file is uploaded to GitHub release
- Ensure release is published (not draft)

**"Update download fails"**
- Verify warehouse has internet access
- Check that release assets are publicly accessible
- Verify firewall allows GitHub downloads

**"Application won't restart after update"**
- Check Windows Event Viewer for errors
- Verify user has permissions to restart app
- Check antivirus isn't blocking Squirrel

### Additional Documentation

- **`RELEASE-PROCESS.md`** - Detailed release procedures and troubleshooting
- **`ICON-AND-INSTALL.md`** - Custom icon setup and PowerShell installation guide
- **`SQUIRREL-SETUP.md`** - Squirrel installation and troubleshooting guide
