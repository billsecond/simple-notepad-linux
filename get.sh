#!/usr/bin/env bash
#
# One-line installer for Simple Notepad.
#
#   curl -fsSL https://raw.githubusercontent.com/billsecond/simple-notepad-linux/main/get.sh | bash
#
# Clones the repo and runs install.sh, which will also install the .NET 10 SDK
# into ~/.dotnet automatically if it isn't already present.

set -euo pipefail

REPO_URL="https://github.com/billsecond/simple-notepad-linux.git"
DEST="${TMPDIR:-/tmp}/simple-notepad-src"

command -v git >/dev/null 2>&1 || { echo "ERROR: git is required. Install git and re-run." >&2; exit 1; }

echo "==> Fetching Simple Notepad…"
rm -rf "$DEST"
git clone --depth 1 "$REPO_URL" "$DEST"

cd "$DEST"
chmod +x install.sh
exec ./install.sh
