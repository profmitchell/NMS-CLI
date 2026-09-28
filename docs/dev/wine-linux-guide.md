# Running NMSE on Linux with Wine

A comprehensive guide to running NMSE (No Man's Save Editor) on Linux using Wine.

> **Note:** NMSE is a Windows app that also runs on Linux/macOS via Wine compatibility layers (AppImage/DMG provided). Options for a native port are under review.

---

## Table of Contents

1. [Overview](#overview)
2. [Prerequisites](#prerequisites)
3. [Option A: Quick Start with Launch Script](#option-a-quick-start-with-launch-script)
4. [Option B: AppImage (Self-Contained)](#option-b-appimage-self-contained)
5. [Option C: Bottles GUI](#option-c-bottles-gui)
6. [Finding Your NMS Save Files](#finding-your-nms-save-files)
7. [Wine Compatibility Notes](#wine-compatibility-notes)
8. [Troubleshooting](#troubleshooting)
9. [Building from Source for Wine](#building-from-source-for-wine)
10. [Known Limitations](#known-limitations)

---

## Overview

NMSE is a .NET 10 WinForms application built for Windows. On Linux, it runs via [Wine](https://www.winehq.org/) - a compatibility layer that translates Windows API calls to Linux equivalents.

**Why does it work well?**
- NMSE uses only standard WinForms controls (no DirectX, no COM, no P/Invoke)
- The .NET runtime is bundled in the self-contained publish (Wine doesn't need .NET installed)
- GDI+ rendering (used for the inventory grid) is well-supported in Wine via FreeType
- All file I/O uses managed .NET APIs, not raw Win32 calls

**Three ways to run NMSE on Linux:**

| Method | Setup Difficulty | Self-Contained | Best For |
|--------|-----------------|----------------|----------|
| **Launch Script** | Easy (needs Wine installed) | No | Users who already have Wine |
| **AppImage** | Easiest (just download + run) | Yes (bundles Wine) | Most users |
| **Bottles** | Easy (GUI) | No | Users who prefer a Wine GUI |

---

## Prerequisites

### System Requirements

- **Architecture:** x86_64 (AMD64) - Wine does not support ARM Linux
- **Distro:** Any modern Linux distribution (Ubuntu 22.04+, Fedora 38+, Arch, openSUSE, etc.)
- **Disk Space:** ~500 MB for Wine + NMSE

### Installing Wine

Wine 9.0 or later is recommended for best .NET WinForms compatibility.

**Ubuntu / Debian / Linux Mint:**
```bash
# Enable 32-bit architecture (required by Wine)
sudo dpkg --add-architecture i386

# Install Wine
sudo apt update
sudo apt install wine64 wine32

# Verify
wine --version
# Should show wine-9.x or later
```

**Fedora:**
```bash
sudo dnf install wine
wine --version
```

**Arch Linux / Manjaro:**
```bash
sudo pacman -S wine
wine --version
```

**openSUSE:**
```bash
sudo zypper install wine
wine --version
```

**WineHQ (Latest stable - all distros):**

For the latest Wine version, use the official WineHQ repository:
https://wiki.winehq.org/Download

---

## Option A: Quick Start with Launch Script

This is the simplest method if you already have Wine installed.

### Step 1: Download NMSE

```bash
# Create a directory for NMSE
mkdir -p ~/NMSE && cd ~/NMSE

# Download the latest Windows build from the GitHub releases page:
#   https://github.com/vectorcmdr/NMSE/releases
#
# Or use the GitHub API to find and download the latest release zip:
DOWNLOAD_URL=$(curl -s https://api.github.com/repos/vectorcmdr/NMSE/releases/latest \
  | grep -o '"browser_download_url": "[^"]*\.zip"' \
  | head -1 | cut -d'"' -f4)
wget "$DOWNLOAD_URL" -O NMSE-latest.zip

# Extract
unzip NMSE-latest.zip -d app/
```

### Step 2: Download the Launch Script

```bash
# Download the launch script
wget https://raw.githubusercontent.com/vectorcmdr/NMSE/main/scripts/linux/nmse.sh
chmod +x nmse.sh
```

### Step 3: Run NMSE

```bash
./nmse.sh
```

The script will:
1. Detect your Wine installation
2. Create a dedicated Wine prefix at `.nmse-wineprefix/`
3. Show your NMS save file location
4. Launch NMSE

### Directory Layout

```
~/NMSE/
├── nmse.sh                # Launch script
├── app/                   # NMSE Windows build
│   ├── NMSE.exe
│   ├── Resources/
│   │   ├── json/
│   │   ├── images/
│   │   ├── icons/
│   │   ├── ui/
│   │   └── map/
│   └── ...
└── .nmse-wineprefix/      # Wine prefix (created on first run)
```

### Launch Script Options

```bash
./nmse.sh                  # Normal launch
./nmse.sh --debug          # Launch with Wine debug logging (writes nmse-wine.log)
./nmse.sh --reset-prefix   # Delete and recreate the Wine prefix
./nmse.sh --winecfg        # Open Wine configuration dialog
./nmse.sh --help           # Show help
```

---

## Option B: AppImage (Self-Contained)

An AppImage bundles NMSE together with Wine into a single executable file. No Wine installation needed - just download, make executable, and run.

### Step 1: Download

```bash
# Resolve the latest AppImage asset from the GitHub API:
DOWNLOAD_URL=$(curl -s https://api.github.com/repos/vectorcmdr/NMSE/releases/latest \
  | grep -o '"browser_download_url": "[^"]*\.AppImage"' \
  | head -1 | cut -d'"' -f4)
wget "$DOWNLOAD_URL" -O NMSE.AppImage
chmod +x NMSE.AppImage
```

### Step 2: Run

```bash
./NMSE.AppImage
```

That's it. The AppImage contains everything needed.

### How It Works

The AppImage internally contains:
- The NMSE Windows build (self-contained .NET)
- A minimal Wine installation
- The `AppRun` entry script that configures and launches everything

The Wine prefix is stored at `~/.local/share/nmse/wineprefix/` and persists across AppImage updates.

### Building Your Own AppImage

If you want to build the AppImage yourself (e.g. from a development build):

```bash
# On Windows: publish NMSE
dotnet publish NMSE.csproj -c Release -r win-x64 --self-contained

# Copy the publish output to your Linux machine, then:
cd scripts/linux/
./build-appimage.sh /path/to/publish/output/
```

See `scripts/linux/build-appimage.sh` for details.

---

## Option C: Bottles GUI

[Bottles](https://usebottles.com) is a user-friendly graphical Wine manager for Linux.

See the dedicated [Bottles Linux Guide](bottles-linux-guide.md) for step-by-step instructions.

---

## Finding Your NMS Save Files

When running NMSE under Wine, you need to navigate to your NMS save files using Wine's path mapping. Wine maps the Linux root filesystem as drive `Z:\`.

### Steam (Standard Proton)

**Linux path:**
```
~/.local/share/Steam/steamapps/compatdata/275850/pfx/drive_c/users/steamuser/AppData/Roaming/HelloGames/NMS
```

**In NMSE's directory browser:**
```
Z:\home\<username>\.local\share\Steam\steamapps\compatdata\275850\pfx\drive_c\users\steamuser\AppData\Roaming\HelloGames\NMS
```

### Steam (Flatpak)

**Linux path:**
```
~/.var/app/com.valvesoftware.Steam/data/Steam/steamapps/compatdata/275850/pfx/drive_c/users/steamuser/AppData/Roaming/HelloGames/NMS
```

**In NMSE:** Navigate via `Z:\home\<username>\.var\app\com.valvesoftware.Steam\...`

### Tips

- The `nmse.sh` launch script automatically detects and displays your save path
- Inside the save directory, select the profile folder (e.g., `st_76561198xxxxxxxxx`)
- After selecting the directory, NMSE will auto-detect save slots

---

## Wine Compatibility Notes

### What Works Well

| Feature | Status | Notes |
|---------|--------|-------|
| All 16 editor tabs (and their sub-panels) | ✅ Works | Tabs, controls, data display all function correctly |
| Save file loading/saving | ✅ Works | .NET file I/O translates seamlessly |
| Inventory grid (custom GDI+ rendering) | ✅ Works | Wine's GDI+ implementation handles this well |
| Icons and images | ✅ Works | PNG loading via .NET managed code |
| Localisation (16 languages) | ✅ Works | JSON file loading, menu switching |
| MessageBox dialogs | ✅ Works | Standard Win32 message boxes |
| File open/save dialogs | ✅ Works | Wine translates to native dialogs (paths show as Z:\\) |
| Clipboard copy/paste | ⚠️ Mostly works | Occasionally quirky between Wine and Linux clipboard |
| Custom font (NMSGeoSans) | ⚠️ Test needed | PrivateFontCollection may need Wine font configuration |
| DPI scaling | ⚠️ Varies | May need manual DPI setting in Wine configuration |

### Wine DLL Overrides

The launch script sets these overrides for best compatibility:

```bash
WINEDLLOVERRIDES="mscoree=d;mshtml=d"
```

- `mscoree=d` - Disables Wine's built-in .NET/Mono (NMSE bundles its own .NET runtime)
- `mshtml=d` - Disables the Gecko HTML engine installer (not needed)

---

## Troubleshooting

### Wine Not Found

```
[NMSE] ERROR: Wine not found.
```

Install Wine 9.0+ using your package manager (see [Prerequisites](#prerequisites)).

### Font Rendering Issues

If fonts look wrong or the NMS custom font doesn't load:

```bash
# Install Windows core fonts
WINEPREFIX=~/.nmse-wineprefix winetricks corefonts

# Or install all common fonts
WINEPREFIX=~/.nmse-wineprefix winetricks allfonts
```

### DPI / Scaling Issues

If the application looks too small or too large:

```bash
# Open Wine configuration
./nmse.sh --winecfg

# In the Graphics tab:
# - Check "Emulate a virtual desktop" for testing
# - Adjust the Screen Resolution (DPI) slider
# - 96 DPI = 100%, 120 DPI = 125%, 144 DPI = 150%
```

### Application Doesn't Start

```bash
# Run with debug logging
./nmse.sh --debug

# Check the log file
cat nmse-wine.log | head -50
```

Common issues:
- **Missing NMSE.exe:** Ensure the `app/` directory contains the published build
- **Wrong Wine version:** Update to Wine 9.0+ (`wine --version`)
- **Corrupted prefix:** Reset with `./nmse.sh --reset-prefix`

### File Dialog Shows Windows Paths

This is normal. Wine maps the Linux filesystem as drive `Z:\`:

| Wine Path | Linux Path |
|-----------|-----------|
| `Z:\` | `/` |
| `Z:\home\user` | `/home/user` |
| `C:\` | `~/.nmse-wineprefix/drive_c/` (Wine's virtual C: drive) |

### Graphical Glitches

If you see rendering artifacts:

```bash
# Try running without display compositing
LIBGL_ALWAYS_SOFTWARE=1 ./nmse.sh

# Or try Xwayland if on Wayland
GDK_BACKEND=x11 ./nmse.sh
```

---

## Building from Source for Wine

NMSE must be published as a self-contained Windows x64 build to run under Wine:

```bash
# On Windows (or WSL2 with .NET SDK):
dotnet publish NMSE.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=false

# The output at Build/bin/Release/net10.0-windows/win-x64/publish/ contains:
# - NMSE.exe (the application)
# - Resources/ (icons, JSON databases, localisation files)
# - *.dll (framework and dependency assemblies)
```

> **Note:** `PublishSingleFile=true` is not recommended for Wine as it can cause issues with resource loading. Keep `PublishSingleFile=false` (the default) for best compatibility.

---

## Known Limitations

These are inherent limitations of running a WinForms app under Wine.

1. **Windows-style UI** - NMSE looks like a Windows application (Windows title bars, Windows-style file dialogs) rather than a native Linux app.

2. **File path display** - Paths are shown in Windows format (`Z:\home\user\...`) rather than Linux format (`/home/user/...`).

3. **No Wayland native support** - Wine uses X11 (or XWayland on Wayland compositors). This usually works transparently but may cause minor issues on pure Wayland setups.

4. **Bundle size** - The AppImage is ~300–500 MB because it includes both Wine and the .NET runtime.

5. **No system tray integration** - Wine's system tray support varies by desktop environment.

6. **Clipboard** - Copy/paste between NMSE and native Linux applications usually works, but occasionally requires a focus change.

---

## Native Port

The Wine compatibility layer works well, but a native Linux build would remove the Wine dependency. Options are under review and there is no current plan.
