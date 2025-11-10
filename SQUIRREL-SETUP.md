# Squirrel Setup Guide

## Why We Switched from `dotnet tool` to `squirrel.exe`

The `dotnet tool install -g Clowd.Squirrel` command has a known bug where the tool package is invalid:

```
Tool 'clowd.squirrel' failed to update due to the following:
The settings file in the tool's NuGet package is invalid: Settings file 'DotnetToolSettings.xml' was not found in the package.
```

**Solution:** We download `squirrel.exe` directly from the NuGet package instead.

## Initial Setup (One-Time)

Run this command from the repository root to download Squirrel:

```powershell
.\download-squirrel.ps1
```

This will:
- Download the Clowd.Squirrel NuGet package
- Extract `squirrel.exe` to the repository root
- Clean up temporary files

**The `squirrel.exe` file is in `.gitignore`**, so it won't be committed to the repository. Each developer needs to run this once.

## Using Squirrel

After running `download-squirrel.ps1`, you can create releases with:

```bash
# Build the application
dotnet publish src\ShipvillanWin\ShipvillanWin.csproj -c Release -r win-x86 --self-contained false -o .\publish

# Create Squirrel release package
.\tools\squirrel.exe pack --packId ShipvillanWin --packVersion 1.0.0 --packDirectory .\publish --releaseDir .\releases
```

This creates:
- `releases/Setup.exe` - Installer for end users
- `releases/ShipvillanWin-1.0.0-full.nupkg` - Full update package
- `releases/ShipvillanWin-1.0.0-delta.nupkg` - Delta update (after first release)
- `releases/RELEASES` - Metadata file

## Compatibility with install.ps1

✅ **YES, fully compatible!**

The `install.ps1` script:
1. Fetches latest release from GitHub API
2. Looks for `Setup.exe` in release assets
3. Downloads and runs `Setup.exe`

It doesn't matter how `Setup.exe` was created (dotnet tool vs direct exe). The script just needs `Setup.exe` to exist in the GitHub Release assets.

**Workflow:**
1. Developer runs `.\squirrel.exe pack` to create `Setup.exe`
2. Developer uploads `Setup.exe` (and other files) to GitHub Release
3. End user runs `install.ps1` which downloads `Setup.exe`
4. `Setup.exe` installs the application

Everything works exactly the same! 🎉

## Troubleshooting

### "squirrel.exe not found" or "Update.exe not found"

Run the download script to get all required tools:
```powershell
.\download-squirrel.ps1
```

This downloads all Squirrel tools to the `tools/` folder, including:
- `squirrel.exe` - Main tool
- `Update.exe` - Required for packaging
- And 20+ other supporting files

### "Access denied" when running squirrel.exe

Make sure you're running PowerShell (not CMD), and the repository isn't in a restricted folder.

### Re-download squirrel.exe

Just run the download script again:
```powershell
.\download-squirrel.ps1
```

It will overwrite the existing file.

## What Gets Committed to Git

✅ **Committed:**
- `download-squirrel.ps1` - Script to download squirrel.exe
- `install.ps1` - End-user installation script

❌ **NOT Committed (in `.gitignore`):**
- `squirrel.exe` - Downloaded locally by each developer
- `releases/` - Build output folder
- `publish/` - Build output folder
- `tools/squirrel/` - Temporary extraction folder

## For CI/CD Pipelines

If you're setting up automated builds (GitHub Actions, Azure DevOps, etc.), add this step:

```yaml
- name: Download Squirrel
  run: .\download-squirrel.ps1
  shell: pwsh

- name: Create Release Package
  run: .\tools\squirrel.exe pack --packId ShipvillanWin --packVersion ${{ env.VERSION }} --packDirectory .\publish --releaseDir .\releases
  shell: pwsh
```

The download script works in CI/CD environments without any special configuration.
