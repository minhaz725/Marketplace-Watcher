#!/usr/bin/env bash
#
# Builds a single self-contained executable containing the API, the background worker and
# the React dashboard. No .NET runtime needed on the target machine.
#
#   ./publish.sh                # for this Mac (osx-arm64)
#   ./publish.sh linux-arm64    # for a Raspberry Pi
#   ./publish.sh win-x64        # for Windows
#
# Output: dist/<rid>/MarketplaceWatcher

set -euo pipefail

RID="${1:-osx-arm64}"
REPO_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$REPO_DIR"

OUT="dist/$RID"
WWWROOT="src/MarketplaceWatcher/wwwroot"

log() { printf '\033[1;34m▸\033[0m %s\n' "$1"; }
die() { printf '\033[1;31m✗\033[0m %s\n' "$1" >&2; exit 1; }

log "Building the React app"
(cd web && npm ci --silent && npm run build)

log "Copying the build into wwwroot"
rm -rf "$WWWROOT"
mkdir -p "$WWWROOT"
cp -R web/dist/. "$WWWROOT/"

log "Publishing for $RID"
# Self-contained so the target needs no .NET install. Deliberately NOT trimmed: EF Core's
# model building, the config binder and System.Text.Json all use reflection the trimmer
# cannot see, so PublishTrimmed removes types that are needed and it fails at runtime.
rm -rf "$OUT"
dotnet publish src/MarketplaceWatcher \
  -c Release \
  -r "$RID" \
  --self-contained \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:EnableCompressionInSingleFile=true \
  -p:DebugType=none \
  -o "$OUT"

# PublishSingleFile silently drops Playwright's Node driver — it is a platform executable,
# not a managed assembly, so it is neither bundled nor left on disk. Without it the app
# starts perfectly, serves the dashboard, and then fails on the first scrape. Restore it
# from the ordinary build output, which does have the right one for this RID.
BUILD_NODE="src/MarketplaceWatcher/bin/Release/net10.0/$RID/.playwright/node"
[[ -d "$BUILD_NODE" ]] || die "Playwright node driver missing from the build at $BUILD_NODE"

log "Restoring Playwright's node driver (single-file publish omits it)"
mkdir -p "$OUT/.playwright/node"
cp -R "$BUILD_NODE/." "$OUT/.playwright/node/"

# Fail loudly rather than shipping something that cannot scrape.
find "$OUT/.playwright/node" -type f -name 'node*' | grep -q . \
  || die "No node binary landed in $OUT/.playwright/node — the build would not be able to scrape"

SIZE="$(du -sh "$OUT" | cut -f1)"

cat <<EOF

▸ Done — $OUT ($SIZE)

  Run it:
    cd $OUT
    ./MarketplaceWatcher

  It serves the dashboard AND the API on http://localhost:5000

  IMPORTANT — secrets. A published build runs in the Production environment, where
  .NET user-secrets are NOT read (they are a development-only convenience). Pass them
  as environment variables instead; the double underscore maps to the config colon:

    export Telegram__BotToken="123456:ABC..."
    export Telegram__ChatId="7880135415"
    ./MarketplaceWatcher

  Chrome still has to be running with the debug port before it can scrape:

    "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome" \\
      --remote-debugging-port=9222 --user-data-dir="\$HOME/.fb-secondary-chrome"

EOF
