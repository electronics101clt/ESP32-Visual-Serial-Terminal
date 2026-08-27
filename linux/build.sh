#!/bin/bash
# Build script for ESP32 Visual Serial Terminal (Linux)
#
# Prerequisites:
#   - .NET 8.0 SDK: https://dotnet.microsoft.com/download
#   - GTK3 and WebKitGTK development libraries:
#       Ubuntu/Debian: sudo apt install libgtk-3-dev libwebkit2gtk-4.0-dev
#       Fedora:        sudo dnf install gtk3-devel webkitgtk4-devel
#       Arch:          sudo pacman -S gtk3 webkit2gtk

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$SCRIPT_DIR/../src/Esp32VisualSerialTerminal.Linux"
OUTPUT_DIR="$SCRIPT_DIR/../bin/linux"

echo "Building ESP32 Visual Serial Terminal for Linux..."

# Check for .NET SDK
if ! command -v dotnet &> /dev/null; then
    echo "Error: .NET SDK not found. Please install .NET 8.0 SDK."
    echo "       https://dotnet.microsoft.com/download"
    exit 1
fi

# Check .NET version
DOTNET_VERSION=$(dotnet --version | cut -d. -f1)
if [ "$DOTNET_VERSION" -lt 8 ]; then
    echo "Error: .NET 8.0 or later is required (found: $(dotnet --version))"
    exit 1
fi

# Build the project
cd "$PROJECT_DIR"
dotnet restore
dotnet publish -c Release -o "$OUTPUT_DIR" --self-contained false

echo ""
echo "Build complete!"
echo "Output: $OUTPUT_DIR/Esp32VisualSerialTerminal"
echo ""
echo "To run: $OUTPUT_DIR/Esp32VisualSerialTerminal"
echo "To install system-wide: sudo ./install.sh"
