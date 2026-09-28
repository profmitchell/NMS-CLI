# Running NMSE on Linux with Bottles

A step-by-step guide to running NMSE (No Man's Save Editor) on Linux using [Bottles](https://usebottles.com), a modern graphical Wine manager.

> **Note:** NMSE is a Windows app that also runs on Linux/macOS via Wine compatibility layers (AppImage/DMG provided). Options for a native port are under review.

---

## Table of Contents

1. [Overview](#overview)
2. [System Requirements](#system-requirements)
3. [Installing Bottles](#installing-bottles)
4. [Setting Up NMSE](#setting-up-nmse)
5. [Finding Your NMS Save Files](#finding-your-nms-save-files)
6. [Troubleshooting](#troubleshooting)
7. [Bottles Configuration Reference](#bottles-configuration-reference)
8. [Comparison: Bottles vs Direct Wine vs AppImage](#comparison-bottles-vs-direct-wine-vs-appimage)

---

## Overview

[Bottles](https://usebottles.com) is a modern, user-friendly Wine manager for Linux with a GTK4 interface. It provides:

- **Graphical bottle management** - create, configure, and manage isolated Wine environments
- **Multiple Wine runners** - choose from Wine, Soda, Caffe, and other runners
- **Dependency management** - install fonts, runtime libraries, and other components
- **Flatpak distribution** - easy, sandboxed installation
- **Per-application configuration** - each app gets its own environment

Bottles is ideal for Linux users who prefer a GUI over command-line Wine management.

---

## System Requirements

| Requirement | Detail |
|------------|--------|
| **Architecture** | x86_64 (AMD64) |
| **Distro** | Any modern Linux distribution |
| **Desktop** | GTK4-compatible (GNOME, XFCE, Cinnamon, MATE, etc.) |
| **Disk space** | ~700 MB (Bottles + Wine runner + NMSE) |

---

## Installing Bottles

### Flatpak (Recommended)

```bash
# Install Flatpak if not already installed
# Ubuntu/Debian:
sudo apt install flatpak
flatpak remote-add --if-not-exists flathub https://dl.flathub.org/repo/flathub.flatpakrepo

# Install Bottles
flatpak install flathub com.usebottles.bottles
```

### Other Methods

- **AUR (Arch/CachyOS):** `paru -S bottles`
- **AppImage:** Available from the Bottles GitHub releases
- **Snap:** `sudo snap install bottles`

---

## Setting Up NMSE

### Step 1: Download NMSE

1. Go to https://github.com/vectorcmdr/NMSE/releases
2. Download the latest `NMSE-<version>-Release.zip` file (listed under the "Latest Build" release)
3. Extract it to a convenient location (e.g. `~/Downloads/NMSE/`)

### Step 2: Create a Bottle

1. Open **Bottles**
2. Click **Create New Bottle** (+)
3. Configure:
   - **Name:** `NMSE`
   - **Environment:** "Application" (not Gaming)
   - **Architecture:** Win64
4. Click **Create**
5. Wait for the bottle to initialise (Bottles will download a Wine runner if needed)

<!-- Screenshot placeholder: Bottles create dialog -->

### Step 3: Add NMSE

1. Select the **NMSE** bottle from the list
2. Click **Add Shortcuts** (or the **Programs** section -> **Add**)
3. Navigate to your extracted NMSE folder and select `NMSE.exe`
4. Click **Open** / **Add**

### Step 4: Launch NMSE

1. In the NMSE bottle, click on `NMSE.exe` in the programs list
2. Click the **Play** button (▶)
3. NMSE will launch - first launch may take 15–30 seconds

<!-- Screenshot placeholder: NMSE running in Bottles -->

---

## Finding Your NMS Save Files

### In the Bottles File Browser

When NMSE opens its file dialog, navigate to your save files via Wine's path mapping:

**Steam (Standard Proton):**
```
Z:\home\<username>\.local\share\Steam\steamapps\compatdata\275850\pfx\drive_c\users\steamuser\AppData\Roaming\HelloGames\NMS
```

**Steam (Flatpak):**
```
Z:\home\<username>\.var\app\com.valvesoftware.Steam\data\Steam\steamapps\compatdata\275850\pfx\drive_c\users\steamuser\AppData\Roaming\HelloGames\NMS
```

### Creating a Shortcut

To make saves easier to find, create a symlink inside the bottle:

1. In Bottles, select the NMSE bottle
2. Click **Browse Files** (opens the bottle's `drive_c` in your file manager)
3. Navigate to `users/<username>/Desktop/`
4. Open a terminal here and create a symlink:
   ```bash
   ln -s ~/.local/share/Steam/steamapps/compatdata/275850/pfx/drive_c/users/steamuser/AppData/Roaming/HelloGames/NMS NMS-Saves
   ```
5. In NMSE, you can now browse to `Desktop/NMS-Saves` from the Wine `C:` drive

### Flatpak Filesystem Access

If using Bottles via Flatpak, you may need to grant filesystem access:

```bash
# Grant access to Steam saves
flatpak override --user --filesystem=~/.local/share/Steam com.usebottles.bottles

# Grant access to Flatpak Steam saves
flatpak override --user --filesystem=~/.var/app/com.valvesoftware.Steam com.usebottles.bottles
```

---

## Troubleshooting

### NMSE Doesn't Launch

1. Ensure the bottle is using a **64-bit** Wine runner (Win64)
2. Try switching runners: Bottle settings -> **Runner** -> select a different one (e.g. Soda or Caffe)
3. Check the Bottles log: click the **Log** tab in the bottle view

### Font Issues

1. In the NMSE bottle, go to **Dependencies**
2. Install **corefonts** (Windows core fonts)
3. Restart NMSE

### DPI / Scaling Issues

1. In the NMSE bottle, go to **Settings**
2. Under **Display**, check **Virtual Desktop** and set a resolution
3. Or: Go to **Wine Configuration** -> **Graphics** -> adjust DPI

### Flatpak Can't Access Save Files

```bash
# Grant Bottles access to your home directory
flatpak override --user --filesystem=home com.usebottles.bottles

# Or grant access to specific directories
flatpak override --user --filesystem=~/.local/share/Steam com.usebottles.bottles
```

### Graphical Glitches

1. In bottle settings, try switching **Renderer** from "OpenGL" to "Vulkan" or vice versa
2. Enable **DXVK** in bottle settings (shouldn't be needed for WinForms, but can help)
3. Try a different Wine runner version

---

## Bottles Configuration Reference

A reference `bottles.yml` is provided at `scripts/linux/bottles.yml` with the recommended settings:

| Setting | Value | Notes |
|---------|-------|-------|
| **Windows Version** | Windows 10 | Best .NET compatibility |
| **Architecture** | win64 | NMSE is a 64-bit application |
| **Runner** | wine-9.0+ / soda-9.0+ | Latest stable runner |
| **WINEDLLOVERRIDES** | `mscoree=d;mshtml=d` | Disable Wine Mono/Gecko installers |
| **Dependencies** | None required | NMSE is self-contained |

---

## Comparison: Bottles vs Direct Wine vs AppImage

| Feature | Bottles | Direct Wine | AppImage |
|---------|---------|-------------|----------|
| **Setup difficulty** | Easy (GUI) | Moderate (CLI) | Easiest (just run) |
| **Self-contained** | No (needs Bottles + Wine) | No (needs Wine) | Yes |
| **GUI management** | ✅ Yes | ❌ No | ❌ No |
| **Per-app configuration** | ✅ Yes | Manual | N/A |
| **Dependency management** | ✅ Yes (GUI) | Manual (winetricks) | Bundled |
| **Update management** | ✅ Runner updates via GUI | Manual | Download new AppImage |
| **Disk usage** | ~700 MB | ~400 MB | ~300–500 MB |
| **Best for** | Users who prefer GUIs | Experienced Linux users | Most users |

For most Linux users, the **AppImage** is the simplest option. **Bottles** is recommended if you prefer a GUI for managing Wine environments or run other Windows applications.

---

## Native Port

Options for a native Linux build are under review; there is no current plan.
