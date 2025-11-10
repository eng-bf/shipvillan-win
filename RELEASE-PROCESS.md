# ShipvillanWin Release Process

This document describes how to create and publish new releases for ShipvillanWin with automatic updates.

## Prerequisites

1. Install Clowd.Squirrel CLI tools:
   ```bash
   dotnet tool install -g Clowd.Squirrel
   ```

2. Ensure you have write access to the GitHub repository: `https://github.com/eng-bf/shipvillan-win`

## Version Numbering

Update the version in `src/ShipvillanWin/ShipvillanWin.csproj`:

```xml
<Version>1.0.0</Version>
<AssemblyVersion>1.0.0</AssemblyVersion>
<FileVersion>1.0.0</FileVersion>
```

Use semantic versioning: `MAJOR.MINOR.PATCH`
- **MAJOR**: Breaking changes
- **MINOR**: New features (backward compatible)
- **PATCH**: Bug fixes

## Building a Release

### Step 1: Build the Application

```bash
dotnet publish src\ShipvillanWin\ShipvillanWin.csproj -c Release -r win-x86 --self-contained false -o .\publish
```

### Step 2: Create Squirrel Release Package

```bash
squirrel pack --packId ShipvillanWin --packVersion 1.0.0 --packDirectory .\publish --releaseDir .\releases
```

This creates:
- `ShipvillanWin-1.0.0-full.nupkg` - Full installer package
- `ShipvillanWin-1.0.0-delta.nupkg` - Delta update (created after first release)
- `RELEASES` - Metadata file listing all available releases
- `Setup.exe` - Installer for end users

### Step 3: Create GitHub Release

1. Go to: https://github.com/eng-bf/shipvillan-win/releases/new
2. Tag version: `v1.0.0`
3. Release title: `ShipvillanWin v1.0.0`
4. Description: List changes, bug fixes, new features
5. Attach these files:
   - `Setup.exe`
   - `ShipvillanWin-1.0.0-full.nupkg`
   - `ShipvillanWin-1.0.0-delta.nupkg` (if exists)
   - `RELEASES`
6. **IMPORTANT**: Keep the repository private, but the release assets will be publicly accessible
7. Click "Publish release"

## How Auto-Updates Work

### Daily Automatic Updates
- Application checks for updates every hour
- Updates are only applied after 3pm PST
- Updates are downloaded and installed silently
- Application automatically restarts to complete the update

### Manual Updates
- IT staff can click "Check for Updates" in the tray menu
- This bypasses the time restriction and checks immediately
- Shows notification balloons with update progress

### Update Process Flow

1. App queries GitHub Releases API: `https://github.com/eng-bf/shipvillan-win/releases`
2. Compares current version with latest release
3. Downloads delta package (only changed files) or full package if needed
4. Extracts and applies update
5. Restarts application

## Rollback Capability

Users can rollback to the previous version:
1. Right-click tray icon
2. Select "Rollback to Previous Version"
3. Confirm the action
4. Application restarts with previous version

**Note**: Squirrel keeps the previous version in the `packages` folder for rollback.

## Testing Releases

### Test Locally Before Publishing

```bash
# Create a test release
squirrel pack --packId ShipvillanWin --packVersion 1.0.1 --packDirectory .\publish --releaseDir .\test-releases

# Start a local web server
cd test-releases
python -m http.server 8080

# Temporarily modify UpdateService.cs to point to localhost:
_updateService = new UpdateService("http://localhost:8080");

# Test the update process
```

### Test on a Single Warehouse Machine

1. Deploy the new release to GitHub
2. Install current version on a test machine
3. Wait for automatic update or trigger manually
4. Verify the update completes successfully
5. Test all functionality after update

## Deployment Checklist

- [ ] Update version numbers in `.csproj`
- [ ] Build and test the application locally
- [ ] Run all integration tests
- [ ] Create release package with Squirrel
- [ ] Test update process locally
- [ ] Create GitHub release with all artifacts
- [ ] Verify release assets are publicly accessible
- [ ] Test update on one warehouse machine
- [ ] Monitor for errors in the first 24 hours
- [ ] Rollback if critical issues discovered

## Troubleshooting

### Updates Not Detected
- Verify RELEASES file is present in GitHub release
- Check that all package files (.nupkg) are uploaded
- Ensure release is published (not draft)
- Check application logs for update check errors

### Update Download Fails
- Verify internet connectivity on warehouse machines
- Check firewall rules allow GitHub access
- Ensure release assets are publicly accessible

### Application Won't Restart
- Check Windows Event Viewer for errors
- Verify user has permissions to restart application
- Check for antivirus blocking Squirrel updater

## Emergency Rollback

If a bad release is deployed:

1. **Immediate**: Notify all warehouses not to update
2. **Create hotfix**: Fix the issue and create new release
3. **Increment version**: Must be higher than bad release
4. **Publish**: Push hotfix as new release
5. **Or delete bad release**: Remove from GitHub (existing installs won't update)

## Security Notes

- Repository is private, but releases are public
- Anyone with the release URL can download updates
- Updates are not signed (consider code signing in the future)
- No authentication required for updates
- HTTPS is used for all downloads from GitHub

## Future Improvements

- [ ] Add code signing for installers and updates
- [ ] Implement staged rollouts (update 10% of machines first)
- [ ] Add update analytics (track update success/failure rates)
- [ ] Create automated build and release pipeline
- [ ] Add SHA256 checksum verification for updates
