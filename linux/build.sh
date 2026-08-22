#!/usr/bin/env bash
#
# Builds a single self-contained executable for Linux.
#
# The result needs no .NET runtime installed and no shared libraries beyond
# what a standard desktop already has, so it can be copied to a machine and
# run directly.

set -euo pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
root="$(cd "$here/.." && pwd)"

arch="${1:-$(uname -m)}"
case "$arch" in
    x86_64|amd64|linux-x64) rid="linux-x64" ;;
    aarch64|arm64|linux-arm64) rid="linux-arm64" ;;
    *) echo "Unsupported architecture: $arch" >&2; exit 1 ;;
esac

out="$root/artifacts/$rid"
project="$root/src/Esp32VisualSerialTerminal.Linux/Esp32VisualSerialTerminal.Linux.vbproj"

if ! command -v dotnet >/dev/null 2>&1; then
    cat >&2 <<'EOF'
The .NET SDK was not found.

On Ubuntu:
    sudo apt update && sudo apt install -y dotnet-sdk-10.0

If that package is unavailable on your release, see:
    https://dotnet.microsoft.com/download/dotnet/10.0
EOF
    exit 1
fi

echo "Building for $rid ..."

dotnet publish "$project" \
    --configuration Release \
    --runtime "$rid" \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -p:EnableCompressionInSingleFile=true \
    -p:DebugType=none \
    --output "$out" \
    --nologo

chmod +x "$out/esp32-visual-serial-terminal"

echo
echo "Built: $out/esp32-visual-serial-terminal"
echo "Run:   $out/esp32-visual-serial-terminal --help"
