# Runbook — Marketplace Watcher

Symptoms → checks → fixes. For *why* things are built this way, see [DECISIONS.md](DECISIONS.md).

## First check, always

```bash
curl -s localhost:5000/api/status | jq
```

```json
{
  "healthy": true,
  "lastSuccessfulPollAt": "2026-07-27T08:14:02Z",
  "minutesSinceLastSuccess": 1.4,
  "lastPollNewCount": 5,
  "consecutiveFailures": 0,
  "lastError": "",
  "activeWatches": 1,
  "seenListings": 214
}
```

| Field | What it tells you |
|---|---|
| `healthy` | `false` = at least one poll failed in a row |
| `minutesSinceLastSuccess` | Should stay under ~2× the poll interval (i.e. < 4 min) |
| `consecutiveFailures` | Drives backoff: 1×, 2×, 4×, 8× the interval, capped at 8× |
| `lastError` | The most recent failure message |

`/health` only says the process is alive. **`/api/status` says whether it is doing its job** — a
process can be perfectly healthy and scraping nothing.

---

## No alerts arriving

### 1. Is it polling at all?

`minutesSinceLastSuccess` climbing past ~4 minutes means polls aren't completing.

### 2. Is Chrome still attached?

The single most common cause — Chrome was closed, restarted, or never started with the debug port.

```bash
curl -s http://localhost:9222/json/version
```

No response → restart it:

```bash
"/Applications/Google Chrome.app/Contents/MacOS/Google Chrome" \
  --remote-debugging-port=9222 --user-data-dir="$HOME/.fb-secondary-chrome"
```

The app recovers on its own once Chrome is back: the poll loop notices the cached CDP connection
has gone stale, logs `CDP connection went stale (Chrome closed or restarted); reconnecting`, and
reconnects on the next poll. No app restart needed.

(Before the reconnect fix this was **not** true — a restarted Chrome left the app failing every
poll forever with `Index was out of range` from `CdpConnection.Context`. If you see that, you are
running a build from before the fix.)

### 3. Is the Facebook session still logged in?

Look in that Chrome window. If it's showing a login wall, log the secondary account back in.
Nothing in the app can detect this: it will scrape a login page and find zero cards. The log line
`No listing cards appeared within 20000ms` is the tell.

### 4. Are matches being filtered out?

Check the log for:

```
Dropped N out-of-region listing(s) for watch X: Swanson, ...
```

If suburbs you care about are listed there, add them to `AllowedLocations` in `appsettings.json`
and restart. The filter is fail-closed — see README.

### 5. Is Telegram failing?

```
N of M alert(s) for watch X failed to send; left unseen and will be retried next poll.
Telegram send failed (400) for ...: {"description":"chat not found"}
```

Verify credentials directly:

```bash
curl -s "https://api.telegram.org/bot<TOKEN>/sendMessage" -d chat_id="<ID>" -d text="test"
```

| Response | Fix |
|---|---|
| `401` / `Not Found` | Wrong token — re-set the user-secret |
| `chat not found` | Wrong chat id, or the bot isn't in the channel |
| `not enough rights` | Add the bot as a channel **administrator** with Post Messages |
| `429 Too Many Requests` | Rate limited — it will retry; expect ~20 msgs/min to a channel |

Nothing is lost while Telegram is down: undelivered listings stay unseen and retry.

---

## Too many alerts

- **A new watch floods on first poll.** Expected — everything is new. Narrow the price band or add
  `excludeKeywords`.
- **Out-of-region listings.** Facebook pads sparse searches nationwide. Tighten `AllowedLocations`,
  or check the ambiguous-name warnings in the README (Avondale, Belmont, Northcote, Hillsborough,
  Milford also exist in other cities).
- **Duplicate alerts for one item.** Expected, rarely: delivery is at-least-once (ADR-002). If the
  *same* item alerts repeatedly every poll, the seller likely edits the title or price — each edit
  is a new content signature by design.

---

## The app exited

It shouldn't. Since the resilience fix, per-watch and per-iteration failures are caught and the
loop continues with backoff.

If the process is genuinely gone, the exception happened outside the poll loop — startup, most
likely. Check the last lines of output:

- `DataAnnotation validation failed for 'TelegramOptions'` → missing `Telegram:BotToken` or
  `Telegram:ChatId` in user-secrets. This is `ValidateOnStart` doing its job.
- A migration error → the DB is in an odd state. It's a local cache, safe to reset:
  ```bash
  rm src/MarketplaceWatcher/watcher.db*   # loses seen-history; the next poll re-alerts everything
  ```

## Verifying a clean stop

```bash
pgrep -fl "bin/Debug/net10.0/MarketplaceWatcher" || echo "not running"
```

Note `pkill -f MarketplaceWatcher.dll` does **not** match — the apphost process is
`MarketplaceWatcher`, no extension. A stale process has been left running by that mistake before.

---

## Scraping suddenly returns nothing

Facebook changed the DOM. Two places to look:

1. `Scraping/CardParser.cs` — the `aria-label` regex. Capture a real label and compare.
2. `Scraping/MarketplaceScraper.cs` — `CardSelector` (`a[href*='/marketplace/item/']`).

Symptom of a *partial* break: `Harvested 0 listings` while Chrome shows a normal feed.

Also watch harvest counts. If they jump back toward ~140 with 12 rounds, `daysSinceListed` has
stopped being honoured — the app still works, it's just scraping the whole feed again. Signature
dedup means you won't get duplicate spam in the meantime.
