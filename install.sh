#!/usr/bin/env bash
#
# Installer for Simple Notepad.
#
#   ./install.sh            # per-user install into ~/.local (no root needed)
#   sudo ./install.sh       # system-wide install into /usr/local
#
# It publishes a self-contained build (no .NET runtime required to run the
# result), drops `notepad` and `notepad.exe` launchers onto your PATH, registers
# a desktop entry + icon, and offers to make Notepad the default text editor.

set -euo pipefail

# --- locate ourselves -------------------------------------------------------
SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" >/dev/null 2>&1 && pwd)"
cd "$SCRIPT_DIR"

APP_ID="simple-notepad"
RID="linux-x64"

# --- choose install locations ----------------------------------------------
if [ "$(id -u)" -eq 0 ]; then
    PREFIX="/usr/local"
    DATA_ROOT="/usr/local/share"
else
    PREFIX="$HOME/.local"
    DATA_ROOT="${XDG_DATA_HOME:-$HOME/.local/share}"
fi

BIN_DIR="$PREFIX/bin"
LIB_DIR="$DATA_ROOT/$APP_ID"            # the published app lives here
ICON_FILE="$LIB_DIR/notepad.png"
APP_DIR="$DATA_ROOT/applications"
DESKTOP_FILE="$APP_DIR/$APP_ID.desktop"

echo "==> Installing Simple Notepad"
echo "    prefix : $PREFIX"
echo "    app    : $LIB_DIR"
echo

# --- find the .NET SDK ------------------------------------------------------
find_dotnet() {
    if command -v dotnet >/dev/null 2>&1; then DOTNET="$(command -v dotnet)"; return 0; fi
    if [ -x "$HOME/.dotnet/dotnet" ]; then DOTNET="$HOME/.dotnet/dotnet"; return 0; fi
    return 1
}

if ! find_dotnet; then
    echo "==> .NET SDK not found — installing .NET 10 into ~/.dotnet (no root needed)…"
    curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
    bash /tmp/dotnet-install.sh --channel 10.0 --install-dir "$HOME/.dotnet"
    DOTNET="$HOME/.dotnet/dotnet"
fi
echo "==> Using dotnet: $DOTNET"

# --- publish a self-contained build ----------------------------------------
echo "==> Building (self-contained, $RID)…"
PUBLISH_DIR="$SCRIPT_DIR/bin/Release/net10.0/$RID/publish"
rm -rf "$PUBLISH_DIR"
"$DOTNET" publish "$SCRIPT_DIR/Notepad.csproj" \
    -c Release -r "$RID" --self-contained true \
    -p:DebugType=none -p:DebugSymbols=false \
    --nologo -v quiet

if [ ! -x "$PUBLISH_DIR/Notepad" ]; then
    echo "ERROR: build did not produce $PUBLISH_DIR/Notepad" >&2
    exit 1
fi

# --- copy files into place --------------------------------------------------
echo "==> Installing files…"
rm -rf "$LIB_DIR"
mkdir -p "$LIB_DIR" "$BIN_DIR" "$APP_DIR"
cp -a "$PUBLISH_DIR/." "$LIB_DIR/"
cp -f "$SCRIPT_DIR/Assets/notepad.png" "$ICON_FILE"

# --- launchers --------------------------------------------------------------
# Linux filesystems are case-sensitive, so install every capitalization the
# user might type: notepad, Notepad, notepad.exe, Notepad.exe.
make_launcher() {
    local target="$1"
    cat > "$target" <<EOF
#!/bin/sh
# Launcher for Simple Notepad
exec "$LIB_DIR/Notepad" "\$@"
EOF
    chmod +x "$target"
}
for name in notepad Notepad notepad.exe Notepad.exe; do
    make_launcher "$BIN_DIR/$name"
done
echo "    launchers: notepad, Notepad, notepad.exe, Notepad.exe (in $BIN_DIR)"

# --- desktop entry ----------------------------------------------------------
cat > "$DESKTOP_FILE" <<EOF
[Desktop Entry]
Type=Application
Version=1.0
Name=Simple Notepad
GenericName=Text Editor
Comment=A simple Notepad clone
Exec=$BIN_DIR/notepad %f
Icon=$ICON_FILE
Terminal=false
Categories=Utility;TextEditor;
MimeType=text/plain;
StartupNotify=true
Keywords=notepad;text;editor;
EOF
echo "    desktop entry: $DESKTOP_FILE"

# also expose the icon to the icon theme, best-effort
THEME_ICON_DIR="$DATA_ROOT/icons/hicolor/512x512/apps"
mkdir -p "$THEME_ICON_DIR" 2>/dev/null && cp -f "$ICON_FILE" "$THEME_ICON_DIR/$APP_ID.png" 2>/dev/null || true

command -v update-desktop-database >/dev/null 2>&1 && update-desktop-database "$APP_DIR" 2>/dev/null || true
command -v gtk-update-icon-cache  >/dev/null 2>&1 && gtk-update-icon-cache -q -t "$DATA_ROOT/icons/hicolor" 2>/dev/null || true

# --- optional: default text editor -----------------------------------------
echo
ANSWER=""
read -r -p "Make Simple Notepad the default application for text files? [y/N] " ANSWER || ANSWER=""
case "$ANSWER" in
    [yY]|[yY][eE][sS])
        if command -v xdg-mime >/dev/null 2>&1; then
            for MIME in \
                text/plain text/markdown text/x-log text/csv \
                application/json application/xml text/x-csrc \
                text/x-python text/x-shellscript ; do
                xdg-mime default "$APP_ID.desktop" "$MIME" 2>/dev/null || true
            done
            echo "    Simple Notepad is now the default for common text types."
        else
            echo "    'xdg-mime' not found; skipped setting the default."
        fi
        ;;
    *)
        echo "    Left the default text editor unchanged."
        ;;
esac

# --- PATH sanity check ------------------------------------------------------
echo
case ":$PATH:" in
    *":$BIN_DIR:"*) : ;;
    *)
        echo "NOTE: $BIN_DIR is not on your PATH."
        echo "      Add this to your ~/.bashrc (or ~/.profile):"
        echo "        export PATH=\"$BIN_DIR:\$PATH\""
        echo "      then restart your shell."
        ;;
esac

echo
echo "==> Done. Try it:"
echo "      notepad"
echo "      notepad.exe myfile.txt"
echo "      notepad ~/notes"
