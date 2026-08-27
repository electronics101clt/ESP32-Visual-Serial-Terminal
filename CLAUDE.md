# ESP32 Visual Serial Terminal - Installation Notes

## Local Installation

**Installed**: 2026-08-27 02:38 AM

### Installation Method

This installation was done **manually** (not using the provided install script).

**Steps taken:**
1. Cloned repo to `/tmp/ESP32-Visual-Serial-Terminal/`
2. Built with `./linux/build.sh`
3. Created symlink manually:
   ```bash
   sudo ln -s /tmp/ESP32-Visual-Serial-Terminal/bin/linux/Esp32VisualSerialTerminal /usr/local/bin/esp32-terminal
   ```

**Command to run**: `esp32-terminal`

### Differences from Official Install Script

**Official install script (`./linux/install.sh`) would have:**
- Installed to `/opt/esp32-visual-serial-terminal/`
- Created symlink as `/usr/local/bin/esp32-visual-serial-terminal` (longer name)
- Added desktop entry to application menu

**This installation:**
- Left in `/tmp/ESP32-Visual-Serial-Terminal/`
- Shorter command name: `esp32-terminal` (instead of `esp32-visual-serial-terminal`)
- Desktop entry added manually: `/usr/share/applications/esp32-visual-serial-terminal.desktop`

### Files

- **Repo location**: `/tmp/ESP32-Visual-Serial-Terminal/`
- **Binary**: `/tmp/ESP32-Visual-Serial-Terminal/bin/linux/Esp32VisualSerialTerminal`
- **Symlink**: `/usr/local/bin/esp32-terminal` → binary
- **Git remote**: https://github.com/electronics101clt/ESP32-Visual-Serial-Terminal.git

### Usage

```bash
esp32-terminal                 # auto-detect, 1024x600
esp32-terminal -s 800x480
esp32-terminal -p /dev/ttyUSB0 -b 115200
esp32-terminal --list-ports
esp32-terminal --help
```

### Rebuild

```bash
cd /tmp/ESP32-Visual-Serial-Terminal
./linux/build.sh
# Symlink already exists, no need to recreate
```
