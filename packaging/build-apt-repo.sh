#!/usr/bin/env bash
#
# Assemble a signed flat APT repository from the .deb(s) in dist/.
#
#   packaging/build-apt-repo.sh [output-dir]
#
# Produces, in the output dir (default: build/aptrepo):
#   *.deb, Packages, Packages.gz, Release, Release.gpg, InRelease,
#   pubkey.gpg (dearmored public key), pubkey.asc, index.html
#
# Signing uses the local GPG key named "Simple Notepad Repository". The PRIVATE
# key never leaves your keyring; only the PUBLIC key is published.

set -euo pipefail

KEY_NAME="Simple Notepad Repository"
ROOT="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." >/dev/null 2>&1 && pwd)"
cd "$ROOT"

OUT="${1:-build/aptrepo}"
PAGES_URL="https://billsecond.github.io/simple-notepad-linux"

command -v dpkg-scanpackages >/dev/null || { echo "ERROR: dpkg-scanpackages (dpkg-dev) missing" >&2; exit 1; }
command -v gpg >/dev/null || { echo "ERROR: gpg missing" >&2; exit 1; }

FPR="$(gpg --list-secret-keys --with-colons "$KEY_NAME" 2>/dev/null | awk -F: '/^fpr:/{print $10; exit}')"
[ -n "$FPR" ] || { echo "ERROR: signing key '$KEY_NAME' not found in keyring" >&2; exit 1; }

ls dist/*.deb >/dev/null 2>&1 || { echo "ERROR: no .deb in dist/ — run packaging/build-deb.sh first" >&2; exit 1; }

echo "==> Building APT repo in $OUT (signing key ${FPR:0:16}…)"
rm -rf "$OUT"
mkdir -p "$OUT"
cp -f dist/*.deb "$OUT/"

cd "$OUT"

# Package index
dpkg-scanpackages --multiversion . /dev/null > Packages
gzip -9 -k -f Packages

# Release file (hand-written; apt-ftparchive isn't required)
hashblock() { # $1 = algo (md5sum|sha256sum)  -> "  <hash> <size> <file>"
    local algo="$1" f
    for f in Packages Packages.gz; do
        printf ' %s %16d %s\n' "$($algo "$f" | cut -d' ' -f1)" "$(stat -c%s "$f")" "$f"
    done
}
{
    echo "Origin: Simple Notepad"
    echo "Label: Simple Notepad"
    echo "Suite: stable"
    echo "Codename: stable"
    echo "Architectures: amd64"
    echo "Components: main"
    echo "Date: $(date -u '+%a, %d %b %Y %H:%M:%S +0000')"
    echo "Description: Simple Notepad APT repository"
    echo "MD5Sum:"
    hashblock md5sum
    echo "SHA256:"
    hashblock sha256sum
} > Release

# Signatures
gpg --batch --yes --default-key "$FPR" --clearsign -o InRelease Release
gpg --batch --yes --default-key "$FPR" -abs -o Release.gpg Release

# Public key for users (binary + armored)
gpg --export "$FPR" > pubkey.gpg
gpg --export --armor "$FPR" > pubkey.asc

# Friendly landing page
cat > index.html <<HTML
<!doctype html><meta charset="utf-8"><title>wdnotepad APT repo</title>
<h1>wdnotepad &mdash; APT repository</h1>
<pre>
sudo install -d /etc/apt/keyrings
curl -fsSL $PAGES_URL/pubkey.gpg | sudo tee /etc/apt/keyrings/wdnotepad.gpg >/dev/null
echo "deb [signed-by=/etc/apt/keyrings/wdnotepad.gpg] $PAGES_URL ./" | sudo tee /etc/apt/sources.list.d/wdnotepad.list
sudo apt update
sudo apt install wdnotepad
</pre>
HTML

echo "==> APT repo ready:"
ls -la
