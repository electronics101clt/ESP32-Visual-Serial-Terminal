#!/usr/bin/env bash
#
# Installs the built executable for the current user.
#
# Everything goes under $HOME, so no root is required. The one thing that does
# need elevation is serial port access, which is a group membership rather than
# an install step and is reported at the end if it is missing.

set -euo pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
root="$(cd "$here/.." && pwd)"

rid="${1:-linux-x64}"
binary="$root/artifacts/$rid/esp32-visual-serial-terminal"

if [[ ! -f "$binary" ]]; then
    echo "No built binary at $binary" >&2
    echo "Build it first:  $here/build.sh" >&2
    exit 1
fi

bindir="$HOME/.local/bin"
appdir="$HOME/.local/share/applications"
icondir="$HOME/.local/share/icons/hicolor/256x256/apps"

mkdir -p "$bindir" "$appdir" "$icondir"

install -m 0755 "$binary" "$bindir/esp32-visual-serial-terminal"
echo "Installed  $bindir/esp32-visual-serial-terminal"

if [[ -f "$here/icon.png" ]]; then
    install -m 0644 "$here/icon.png" "$icondir/esp32-visual-serial-terminal.png"
    echo "Installed  $icondir/esp32-visual-serial-terminal.png"
fi

sed "s|@BIN@|$bindir/esp32-visual-serial-terminal|g" \
    "$here/esp32-visual-serial-terminal.desktop" \
    > "$appdir/esp32-visual-serial-terminal.desktop"
chmod 0644 "$appdir/esp32-visual-serial-terminal.desktop"
echo "Installed  $appdir/esp32-visual-serial-terminal.desktop"

if command -v update-desktop-database >/dev/null 2>&1; then
    update-desktop-database "$appdir" >/dev/null 2>&1 || true
fi
if command -v gtk-update-icon-cache >/dev/null 2>&1; then
    gtk-update-icon-cache -f -t "$HOME/.local/share/icons/hicolor" >/dev/null 2>&1 || true
fi

echo

# ~/.local/bin is on PATH by default on Ubuntu, but only if it existed when the
# session started. Saying so is more useful than letting the command appear
# missing after a successful install.
case ":$PATH:" in
    *":$bindir:"*) ;;
    *) echo "Note: $bindir is not on PATH for this shell. Log out and back in, or:"
       echo "        export PATH=\"\$HOME/.local/bin:\$PATH\""
       echo ;;
esac

if ! id -nG "$USER" | tr ' ' '\n' | grep -qx dialout; then
    echo "Serial devices belong to the 'dialout' group, which you are not in."
    echo "Opening a port will be refused until you join it:"
    echo "        sudo usermod -aG dialout $USER"
    echo "    then log out and back in."
    echo
fi

echo "Done.  Run: esp32-visual-serial-terminal --help"
