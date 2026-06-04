#!/usr/bin/env bash
#
# Redeploy Simple Notepad end to end:
#   1. (re)install the app locally
#   2. build the .deb
#   3. build the signed APT repo
#   4. push source to main
#   5. publish the APT repo to the gh-pages branch
#   6. ensure GitHub Pages is enabled
#
#   ./deploy.sh [version]            # version defaults to 1.0.0
#   SKIP_LOCAL=1 ./deploy.sh         # skip the local reinstall
#
# SECURITY: the GitHub token is read from $GITHUB_TOKEN or ~/Desktop/git.txt at
# runtime and is NEVER written to the repo, git config, or command output (it is
# scrubbed). The GPG PRIVATE key stays in your keyring; only the public key is
# published. Secrets and build artifacts are covered by .gitignore.

set -euo pipefail

REPO_SLUG="${REPO_SLUG:-billsecond/simple-notepad-linux}"
PAGES_URL="https://billsecond.github.io/simple-notepad-linux"
VERSION="${1:-1.0.0}"

ROOT="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" >/dev/null 2>&1 && pwd)"
cd "$ROOT"

# --- token (never persisted) ---
TOKEN="${GITHUB_TOKEN:-}"
if [ -z "$TOKEN" ] && [ -f "$HOME/Desktop/git.txt" ]; then
    TOKEN="$(tr -d ' \t\r\n' < "$HOME/Desktop/git.txt")"
fi
[ -n "$TOKEN" ] || { echo "ERROR: set GITHUB_TOKEN or place a token in ~/Desktop/git.txt" >&2; exit 1; }
scrub() { sed "s/${TOKEN}/***TOKEN***/g"; }

export PATH="$HOME/.dotnet:$PATH" DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1

echo "==> [1/6] Local install"
if [ "${SKIP_LOCAL:-0}" = "1" ]; then echo "    (skipped)"; else ./install.sh < /dev/null; fi

echo "==> [2/6] Build .deb"
bash packaging/build-deb.sh "$VERSION"

echo "==> [3/6] Build signed APT repo"
bash packaging/build-apt-repo.sh build/aptrepo

echo "==> [4/7] Sync snap version to $VERSION"
sed -i -E "s/^version: .*/version: '$VERSION'/" snap/snapcraft.yaml
echo "    snap/snapcraft.yaml -> $(grep -E '^version:' snap/snapcraft.yaml)"

echo "==> [5/7] Commit & push source (main) — triggers the snap build/publish CI"
git add -A
git commit -q -m "Deploy v$VERSION" || echo "    (no source changes to commit)"
git push "https://${TOKEN}@github.com/${REPO_SLUG}.git" main 2>&1 | scrub

echo "==> [6/7] Publish APT repo to gh-pages (force)"
GHP="$(mktemp -d)"
cp -a build/aptrepo/. "$GHP/"
touch "$GHP/.nojekyll"          # serve files verbatim (no Jekyll processing)
git -C "$GHP" init -q
git -C "$GHP" checkout -q -b gh-pages
git -C "$GHP" add -A
git -C "$GHP" -c user.name="Simple Notepad Deploy" -c user.email="william@daugherty.info" \
    commit -q -m "apt repo v$VERSION"
git -C "$GHP" push -q --force "https://${TOKEN}@github.com/${REPO_SLUG}.git" gh-pages 2>&1 | scrub
rm -rf "$GHP"

echo "==> [7/7] Ensure GitHub Pages is enabled"
curl -s -o /dev/null -w '    pages api: %{http_code}\n' -X POST \
    -H "Authorization: token $TOKEN" -H "Accept: application/vnd.github+json" \
    "https://api.github.com/repos/${REPO_SLUG}/pages" \
    -d '{"source":{"branch":"gh-pages","path":"/"}}' || true

cat <<EOF

==> Done (v$VERSION).

  Snap (publishes via CI once SNAPCRAFT_STORE_CREDENTIALS secret is set):
    sudo snap install wdnotepad

  APT:
    sudo install -d /etc/apt/keyrings
    curl -fsSL $PAGES_URL/pubkey.gpg | sudo tee /etc/apt/keyrings/wdnotepad.gpg >/dev/null
    echo "deb [signed-by=/etc/apt/keyrings/wdnotepad.gpg] $PAGES_URL ./" | sudo tee /etc/apt/sources.list.d/wdnotepad.list
    sudo apt update && sudo apt install wdnotepad

  Snap build status: https://github.com/${REPO_SLUG}/actions
EOF
