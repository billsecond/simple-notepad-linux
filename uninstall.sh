#!/usr/bin/env bash
#
# Uninstaller for Simple Notepad. Use `sudo ./uninstall.sh` if you installed
# system-wide.

set -euo pipefail

APP_ID="simple-notepad"

if [ "$(id -u)" -eq 0 ]; then
    PREFIX="/usr/local"; DATA_ROOT="/usr/local/share"
else
    PREFIX="$HOME/.local"; DATA_ROOT="${XDG_DATA_HOME:-$HOME/.local/share}"
fi

BIN_DIR="$PREFIX/bin"

echo "==> Removing Simple Notepad"
rm -rf "$DATA_ROOT/$APP_ID"
rm -f  "$BIN_DIR/notepad" "$BIN_DIR/Notepad" "$BIN_DIR/notepad.exe" "$BIN_DIR/Notepad.exe"
rm -f  "$DATA_ROOT/applications/$APP_ID.desktop"
rm -f  "$DATA_ROOT/icons/hicolor/512x512/apps/$APP_ID.png"

command -v update-desktop-database >/dev/null 2>&1 && \
    update-desktop-database "$DATA_ROOT/applications" 2>/dev/null || true

echo "==> Done."
