# Marketplace Watcher

A **.NET 10 (C#)** service that watches **Facebook Marketplace**, filters listings against
configurable searches, dedupes them, and sends **Telegram** alerts for genuinely new items.

A .NET port of an earlier Java/Spring project (*House Hunt 2026*).

> **Status:** the backend works end-to-end and runs daily. The React dashboard (Phase 13) and
> CI (Phase 12) are not built yet — see [PROGRESS.md](PROGRESS.md) for exactly where things are.

## What it does

- **Dynamic watches** — created and stopped at runtime over the API (a subscription model), each
  with its own price range and include/exclude keyword filters. No restart needed.
- **Gentle scraping** — a background loop polls on a jittered 60–120s interval by *attaching* to a
  Chrome session over CDP. It never launches a browser, never logs in, and never touches
  credentials. One page at a time, sequentially.
- **Card parsing** — reads each card's `aria-label`, which carries clean
  `{title}, NZ${price}, {location}, listing {id}` text. Harvests the virtualized feed by scrolling
  until two consecutive rounds yield nothing new.
- **Recency filter** — only scrapes listings posted in the last day (`daysSinceListed`).
- **Location filter** — Facebook pads sparse searches with nationwide results, so listings are
  filtered against a configured list of local place names.
- **Content-signature dedup** — a SHA-256 of normalized `title|price|location`, deliberately
  *excluding* the URL, so a relisted item doesn't alert twice.
- **At-least-once Telegram alerts** — a listing is only recorded as seen once its alert actually
  sent, so an outage delays alerts rather than losing them.

## Architecture

```
                    ASP.NET Core Minimal APIs  (/health, /api/status, /api/watches, /api/poll)
                                   │
      ┌────────────────────────────┼────────────────────────────┐
      ▼                            ▼                            ▼
 BackgroundService          EF Core + SQLite             Telegram notifier
 (jittered poll loop)       (watches + seen store)       (typed HttpClient + Polly)
      │
      ▼
 Playwright (CDP attach) ─▶ parse aria-labels ─▶ location filter ─▶ watch filters ─▶ dedupe ─▶ alert
```

## Prerequisites

- .NET 10 SDK
- Google Chrome
- A Telegram bot token (via [@BotFather](https://t.me/BotFather)) and a chat/channel id

## Run it

**1. Start the secondary-account Chrome** (a dedicated profile, logged in to the Facebook account
you want to watch with — never your primary):

```bash
"/Applications/Google Chrome.app/Contents/MacOS/Google Chrome" \
  --remote-debugging-port=9222 \
  --user-data-dir="$HOME/.fb-secondary-chrome"
```

Sanity check it's listening: `curl http://localhost:9222/json/version`

**2. Set your secrets** (stored outside the repo, never committed):

```bash
dotnet user-secrets set "Telegram:BotToken" "<token>" --project src/MarketplaceWatcher
dotnet user-secrets set "Telegram:ChatId"  "<chat-id>" --project src/MarketplaceWatcher
```

> `dotnet user-secrets` resolves the project from the current directory, so from the repo root you
> must pass `--project` — the root only has a `.slnx`, which carries no `UserSecretsId`.

**3. Run:**

```bash
dotnet run --project src/MarketplaceWatcher
```

**4. Add a watch:**

```bash
curl -X POST http://localhost:5000/api/watches \
  -H "Content-Type: application/json" \
  -d '{"name":"sofa","minPrice":0,"maxPrice":200,"excludeKeywords":["wanted","parts"]}'
```

The poll loop wakes immediately — no waiting for the interval.

## API

| Endpoint | Purpose |
|---|---|
| `GET /health` | Liveness — is the process up |
| `GET /api/status` | Is it *working* — last poll, failures, counts |
| `GET /api/watches` | List active watches |
| `POST /api/watches` | Create a watch (triggers an immediate poll) |
| `DELETE /api/watches/{id}` | Soft-disable a watch |
| `POST /api/poll` | Trigger an immediate poll |

## Configuration

`src/MarketplaceWatcher/appsettings.json`:

| Key | Meaning |
|---|---|
| `PollIntervalSeconds` | Base interval; jittered ±30s (default 90) |
| `DaysSinceListed` | Only scrape listings this recent (default 1; null disables) |
| `AllowedLocations` | Place names a listing must match. **Empty disables the filter** |

### A note on `AllowedLocations`

Matching is case-insensitive **substring**, so `"Mangere"` also covers `"Mangere Bridge"`, and
`"Te Atatu"` covers both Peninsula and South. The shipped list covers suburbs within roughly 15km
of Auckland CBD.

It is **fail-closed**: a local suburb whose name isn't in the list gets dropped. Every poll logs
the distinct locations it rejected, so watch for `Dropped N out-of-region listing(s): ...` and add
anything you're missing.

⚠️ These names also exist in other NZ cities, so they may occasionally let a non-Auckland listing
through: **Avondale** (Christchurch), **Hillsborough** (Christchurch), **Northcote** (Christchurch),
**Belmont** (Lower Hutt), **Milford** (Fiordland). Remove them if the noise bothers you more than
the misses.

### What is *not* configurable

**Search radius.** Facebook ignores `radius` in the URL and applies the location saved on your
account instead. Change it in the Marketplace app, not here. See [DECISIONS.md](DECISIONS.md).

## Development

```bash
dotnet build
dotnet test tests/MarketplaceWatcher.Tests    # 64 tests
dotnet format --verify-no-changes
```

Pure logic (parsing, filters, dedup, backoff, URL building) is written test-first; anything
requiring Playwright is verified by running against the live site.

## Project layout

```
marketplace-watcher/
├─ MarketplaceWatcher.slnx
├─ CLAUDE.md · PROGRESS.md · DECISIONS.md · RUNBOOK.md
├─ src/MarketplaceWatcher/
│  ├─ Program.cs                    # host startup + service wiring
│  ├─ Api/                          # minimal-API endpoints
│  ├─ Config/                       # Options POCOs
│  ├─ Data/                         # EF Core context, watches + seen store, migrations
│  ├─ Domain/                       # Listing record + content signature
│  ├─ Notifications/                # INotifier + Telegram
│  ├─ Scraping/                     # CDP, parsing, filters, harvest policy
│  └─ Worker/                       # polling loop, status, backoff, poll signal
└─ tests/MarketplaceWatcher.Tests/
```

## Operations

See [RUNBOOK.md](RUNBOOK.md) for symptoms → checks → fixes.
