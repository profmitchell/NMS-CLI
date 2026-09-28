# NMSE macOS Scripts

Scripts and configuration files for running NMSE on macOS via Wine.

## Contents

| File | Description |
|------|-------------|
| `build-dmg.sh` | CI script - builds a macOS DMG containing NMSE.app with a Wine launcher |
| `README.md` | This file |

## macOS Requirements

NMSE is a Windows WinForms application. On macOS it runs through a Wine compatibility
layer. Supported options:

- **Gcenx Wine Builds** (free, recommended) - current Wine with Apple Silicon and Intel support.
- **CrossOver 26 or later** (paid, CodeWeavers) - current Wine base with commercial support.

Whisky and Homebrew Wine builds are not supported: Whisky is no longer maintained and
both lag behind the Wine version NMSE needs.

**Setup:** follow the [Gcenx Wine Builds Guide](../../docs/dev/gcenx-macos-guide.md) or
the [CrossOver macOS Guide](../../docs/dev/crossover-macos-guide.md).

## NMS Save File Location on macOS

| Installation | Path |
|-------------|------|
| Steam (native macOS) | `~/Library/Application Support/HelloGames/NMS/<profile>/` |
| Steam (via Wine) | Inside the Wine prefix under `drive_c/users/<user>/AppData/Roaming/HelloGames/NMS/` |

## Full Documentation

- [Gcenx Wine Builds Guide](../../docs/dev/gcenx-macos-guide.md) - free, recommended setup for macOS
- [CrossOver macOS Guide](../../docs/dev/crossover-macos-guide.md) - paid, supported alternative
- [Wine Linux Guide](../../docs/dev/wine-linux-guide.md) - Linux equivalent (different Wine stack)
