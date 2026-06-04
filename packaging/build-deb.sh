#!/usr/bin/env bash
#
# Build a .deb package for Simple Notepad (self-contained, no runtime needed).
#
#   packaging/build-deb.sh [version]
#
# Output: dist/simple-notepad_<version>_amd64.deb

set -euo pipefail

VERSION="${1:-1.0.0}"
ARCH="amd64"
RID="linux-x64"
PKG="simple-notepad"

ROOT="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." >/dev/null 2>&1 && pwd)"
cd "$ROOT"

DOTNET="$(command -v dotnet || echo "$HOME/.dotnet/dotnet")"
[ -x "$DOTNET" ] || { echo "ERROR: dotnet not found" >&2; exit 1; }

STAGE="$(mktemp -d)"
PUBLISH="$STAGE/publish"
PKGDIR="$STAGE/pkg"
trap 'rm -rf "$STAGE"' EXIT

echo "==> Publishing self-contained build…"
"$DOTNET" publish Notepad.csproj -c Release -r "$RID" --self-contained true \
    -p:InvariantGlobalization=true -p:DebugType=none -p:DebugSymbols=false \
    -o "$PUBLISH" --nologo -v quiet

# ---- lay out the package tree -------------------------------------------------
APPDIR="$PKGDIR/usr/lib/$PKG"
BINDIR="$PKGDIR/usr/bin"
DESKTOPDIR="$PKGDIR/usr/share/applications"
ICONDIR="$PKGDIR/usr/share/icons/hicolor/512x512/apps"
mkdir -p "$APPDIR" "$BINDIR" "$DESKTOPDIR" "$ICONDIR" "$PKGDIR/DEBIAN"

cp -a "$PUBLISH/." "$APPDIR/"
cp -f "Assets/notepad.png" "$ICONDIR/$PKG.png"

# launchers: every capitalization, with or without a filename
for name in notepad Notepad notepad.exe Notepad.exe; do
    cat > "$BINDIR/$name" <<EOF
#!/bin/sh
exec "/usr/lib/$PKG/Notepad" "\$@"
EOF
    chmod 0755 "$BINDIR/$name"
done

cat > "$DESKTOPDIR/$PKG.desktop" <<EOF
[Desktop Entry]
Type=Application
Version=1.0
Name=Simple Notepad
GenericName=Text Editor
Comment=A simple Notepad clone
Exec=/usr/bin/notepad %f
Icon=$PKG
Terminal=false
Categories=Utility;TextEditor;
MimeType=text/plain;
StartupNotify=true
Keywords=notepad;text;editor;
EOF

INSTALLED_KB="$(du -sk "$PKGDIR/usr" | cut -f1)"

cat > "$PKGDIR/DEBIAN/control" <<EOF
Package: $PKG
Version: $VERSION
Section: editors
Priority: optional
Architecture: $ARCH
Depends: libc6, libstdc++6, libgcc-s1, zlib1g, libfontconfig1, libx11-6, libice6, libsm6
Installed-Size: $INSTALLED_KB
Maintainer: William Daugherty <william@daugherty.info>
Homepage: https://github.com/billsecond/simple-notepad-linux
Description: Simple Notepad - a lightweight Notepad clone
 A faithful clone of the classic Windows Notepad, built with .NET 10 and
 Avalonia (XAML). File/Edit/Format/View menus, find and replace, go-to-line,
 word wrap, font picker, zoom, a system theme with light/dark override, and
 persisted settings. Self-contained: no .NET runtime required.
EOF

cat > "$PKGDIR/DEBIAN/postinst" <<'EOF'
#!/bin/sh
set -e
command -v update-desktop-database >/dev/null 2>&1 && update-desktop-database -q /usr/share/applications || true
command -v gtk-update-icon-cache  >/dev/null 2>&1 && gtk-update-icon-cache -q -t /usr/share/icons/hicolor || true
exit 0
EOF

cat > "$PKGDIR/DEBIAN/postrm" <<'EOF'
#!/bin/sh
set -e
command -v update-desktop-database >/dev/null 2>&1 && update-desktop-database -q /usr/share/applications || true
exit 0
EOF
chmod 0755 "$PKGDIR/DEBIAN/postinst" "$PKGDIR/DEBIAN/postrm"

# ---- build --------------------------------------------------------------------
mkdir -p "$ROOT/dist"
DEB="$ROOT/dist/${PKG}_${VERSION}_${ARCH}.deb"
dpkg-deb --build --root-owner-group "$PKGDIR" "$DEB"

echo "==> Built: $DEB"
dpkg-deb --info "$DEB" | sed 's/^/    /'
