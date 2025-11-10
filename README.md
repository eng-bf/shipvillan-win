# ShipvillanWin (x86, .NET 8)

A minimal WinForms tray application that:
- Runs in the background with a system tray icon
- Provides a **Hello World** menu item that shows a popup
- Registers itself to **start at user login** (HKCU\...\Run)
- Targets **x86** (32-bit) Windows

## Prerequisites

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
