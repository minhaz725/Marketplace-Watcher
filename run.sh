#!/usr/bin/env bash
#
# Starts everything Marketplace Watcher needs, in order:
#   1. Chrome with remote debugging (the secondary Facebook account profile)
#   2. The .NET backend  — http://localhost:5000
#   3. The Vite frontend — http://localhost:5173
#
# Ctrl-C stops the backend and frontend. Chrome is only stopped if this script
# started it — if it was already running, it is left alone.

set -euo pipefail

CDP_PORT=9222
API_PORT=5000
UI_PORT=5173
CHROME="/Applications/Google Chrome.app/Contents/MacOS/Google Chrome"
CHROME_PROFILE="$HOME/.fb-secondary-chrome"

REPO_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$REPO_DIR"

BACKEND_PID=""
FRONTEND_PID=""
CHROME_PID=""

log()  { printf '\033[1;34m▸\033[0m %s\n' "$1"; }
warn() { printf '\033[1;33m!\033[0m %s\n' "$1"; }
die()  { printf '\033[1;31m✗\033[0m %s\n' "$1" >&2; exit 1; }

# Kill a process and everything it spawned. Necessary because `dotnet run` launches the
# real app as a SEPARATE child process (bin/Debug/.../MarketplaceWatcher) and `npm run dev`
# launches vite the same way — killing only the PID we captured leaves the actual work
# running, which is exactly what happened the first time this script was tested.
kill_tree() {
  local pid="$1"
  [[ -z "$pid" ]] && return 0
  local child
  for child in $(pgrep -P "$pid" 2>/dev/null); do
    kill_tree "$child"
  done
  kill "$pid" 2>/dev/null || true
}

CLEANED=0

cleanup() {
  # trap fires on both TERM and EXIT, so guard against running twice.
  [[ "$CLEANED" == "1" ]] && return 0
  CLEANED=1

  echo
  log "Shutting down…"
  kill_tree "$FRONTEND_PID"
  kill_tree "$BACKEND_PID"

  # No `pkill -f <path>` sweep here on purpose. It only runs inside cleanup, so it cannot
  # help in the one case it was meant for (the script dying without running cleanup), and
  # `-f` matches any command line containing the string — including the shell that invoked
  # this script. That is not hypothetical: it killed the terminal during testing.

  # Only stop Chrome if we were the ones who started it. Killing a browser the
  # user already had open would be rude and would close their tabs.
  if [[ -n "$CHROME_PID" ]]; then
    log "Stopping the Chrome we started (pid $CHROME_PID)"
    kill_tree "$CHROME_PID"
  fi

  wait 2>/dev/null || true
  log "Done."
}
trap cleanup EXIT INT TERM

port_in_use() { lsof -nP -iTCP:"$1" -sTCP:LISTEN >/dev/null 2>&1; }
cdp_ready()   { curl -fsS --max-time 2 "http://localhost:$CDP_PORT/json/version" >/dev/null 2>&1; }

# ── 1. Chrome ────────────────────────────────────────────────────────────────
if cdp_ready; then
  log "Chrome already listening on :$CDP_PORT — reusing it"
else
  # Gotcha worth catching: if Chrome is already running with this profile but
  # WITHOUT the debug flag, launching again just opens a tab in the existing
  # instance and CDP never comes up. Re-running the command would look like it
  # worked while the port stayed closed.
  if pgrep -f "user-data-dir=$CHROME_PROFILE" >/dev/null 2>&1; then
    die "Chrome is running with that profile but no debug port.
   Quit that Chrome window, then run this script again."
  fi

  [[ -x "$CHROME" ]] || die "Chrome not found at: $CHROME"

  log "Starting Chrome (profile: $CHROME_PROFILE)"
  "$CHROME" \
    --remote-debugging-port="$CDP_PORT" \
    --user-data-dir="$CHROME_PROFILE" \
    >/dev/null 2>&1 &
  CHROME_PID=$!

  for _ in {1..30}; do
    cdp_ready && break
    sleep 0.5
  done

  cdp_ready || die "Chrome did not open the debug port within 15s"
  log "Chrome ready on :$CDP_PORT"
  warn "Make sure that window is logged in to the secondary Facebook account."
fi

# ── 2. Backend ───────────────────────────────────────────────────────────────
if port_in_use "$API_PORT"; then
  die "Port $API_PORT is already in use — the backend is probably already running.
   Stop it with:  pkill -f 'bin/Debug/net10.0/MarketplaceWatcher'"
fi

log "Starting backend on :$API_PORT"
dotnet run --project src/MarketplaceWatcher &
BACKEND_PID=$!

for _ in {1..60}; do
  curl -fsS --max-time 2 "http://localhost:$API_PORT/health" >/dev/null 2>&1 && break
  # Fail fast if it died on startup rather than waiting out the full timeout.
  kill -0 "$BACKEND_PID" 2>/dev/null || die "Backend exited during startup — see the output above"
  sleep 1
done
log "Backend healthy"

# ── 3. Frontend ──────────────────────────────────────────────────────────────
if port_in_use "$UI_PORT"; then
  warn "Port $UI_PORT is in use — Vite will pick the next free port; watch its output"
fi

if [[ ! -d web/node_modules ]]; then
  log "Installing frontend dependencies (first run)"
  (cd web && npm ci)
fi

# This Mac's (managed) firewall blocks incoming connections to the default Homebrew `node`,
# so the dashboard is unreachable over WiFi. node@22 is on the firewall's allow list, so put
# it first on PATH for the frontend (vite's `#!/usr/bin/env node` then resolves to it).
# Falls back to the default node if node@22 isn't installed.
NODE22_BIN="$(brew --prefix node@22 2>/dev/null)/bin"
if [[ -x "$NODE22_BIN/node" ]]; then
  log "Using node@22 for the frontend (firewall-allowed): $("$NODE22_BIN/node" -v)"
  export PATH="$NODE22_BIN:$PATH"
else
  warn "node@22 not found — using default node; WiFi access may be blocked by the firewall"
fi

log "Starting frontend"
(cd web && npm run dev) &
FRONTEND_PID=$!

sleep 3

LAN_IP="$(ipconfig getifaddr en0 2>/dev/null || ipconfig getifaddr en1 2>/dev/null || true)"
echo
log "Running:"
echo "     dashboard   http://localhost:$UI_PORT"
[[ -n "$LAN_IP" ]] && echo "     on wifi     http://$LAN_IP:$UI_PORT"
echo "     api         http://localhost:$API_PORT/api/status"
echo
log "Ctrl-C to stop."

# Wait on the backend; if it dies, the trap tears everything else down.
wait "$BACKEND_PID"
