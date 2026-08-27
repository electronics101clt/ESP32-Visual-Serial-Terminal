#!/bin/bash
# Install script for ESP32 Visual Serial Terminal (Linux)
#
# Run with: sudo ./install.sh
#
# Installs to /opt/esp32-visual-serial-terminal with a desktop entry
# and symlink in /usr/local/bin

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BIN_DIR="$SCRIPT_DIR/../bin/linux"
INSTALL_DIR="/opt/esp32-visual-serial-terminal"
DESKTOP_FILE="/usr/share/applications/esp32-visual-serial-terminal.desktop"
SYMLINK="/usr/local/bin/esp32-visual-serial-terminal"

# Check if running as root
if [ "$EUID" -ne 0 ]; then
    echo "Please run as root: sudo ./install.sh"
    exit 1
fi

# Check if build exists
if [ ! -f "$BIN_DIR/Esp32VisualSerialTerminal" ]; then
    echo "Error: Build not found. Run ./build.sh first."
    exit 1
fi

echo "Installing ESP32 Visual Serial Terminal..."

# Create installation directory
mkdir -p "$INSTALL_DIR"

# Copy files
cp -r "$BIN_DIR"/* "$INSTALL_DIR/"

# Make executable
chmod +x "$INSTALL_DIR/Esp32VisualSerialTerminal"

# Create symlink
ln -sf "$INSTALL_DIR/Esp32VisualSerialTerminal" "$SYMLINK"

# Install desktop entry
cat > "$DESKTOP_FILE" << 'EOF'
[Desktop Entry]
Name=ESP32 Visual Serial Terminal
Comment=Renders HTML pushed by a microcontroller over serial
Exec=/opt/esp32-visual-serial-terminal/Esp32VisualSerialTerminal
Icon=utilities-terminal
Terminal=false
Type=Application
Categories=Development;Electronics;
Keywords=ESP32;serial;terminal;microcontroller;
EOF

# Update desktop database
if command -v update-desktop-database &> /dev/null; then
    update-desktop-database /usr/share/applications/ 2>/dev/null || true
fi

echo ""
echo "Installation complete!"
echo ""
echo "You can now:"
echo "  - Run from terminal: esp32-visual-serial-terminal"
echo "  - Find in application menu: ESP32 Visual Serial Terminal"
echo ""
echo "Note: Add your user to the 'dialout' group for serial port access:"
echo "      sudo usermod -a -G dialout \$USER"
echo "      (Log out and back in for the change to take effect)"
