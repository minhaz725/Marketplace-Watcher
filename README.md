# Marketplace Watcher

An event-driven **.NET 10 (C#) + React** service that watches **Facebook Marketplace**, filters
listings against configurable searches, dedupes them, and sends **Telegram** alerts for genuinely
new items — fronted by a small React dashboard.

A .NET port of an earlier Java/Spring project (*House Hunt 2026*).

## What it does
- **Configurable watches** — a broad "free stuff near me" search plus keyword searches (e.g. *sofa*,
  *iPhone 16 Pro Max*), each with its own price range, radius, and include/exclude keyword filters.
- **Gentle scraping** — a background loop polls on a jittered interval by *attaching* to a Chrome
  session over CDP (never launching or logging in), then reads the live Marketplace feed.
- **Robust parsing** — harvests virtualized feed cards as they render, reconstructs CSS-shuffled
  decoy text by on-screen position, and computes a normalized **content signature** per listing.
- **Smart dedup** — signatures are stored in a database so you're only alerted about *new* listings,
  not the same item re-surfacing.
- **Telegram alerts** — new matches are pushed to your phone with title, price, location, and link.
- **React dashboard** — browse past listings, view per-watch status, and trigger an immediate poll.

## Architecture
```
 React + TS UI  ──HTTP──▶  ASP.NET Core Minimal APIs
                                   │
      ┌────────────────────────────┼────────────────────────────┐
      ▼                            ▼                             ▼
 BackgroundService          EF Core + SQLite              Telegram notifier
 (polling loop)             (seen-signature store)        (typed HttpClient + Polly)
      │
      ▼
 Playwright (CDP attach) ─▶ parse cards ─▶ filter ─▶ dedupe ─▶ alert
```

## Tech stack
- **.NET 10 / C# 14** — Generic Host running a `BackgroundService` polling loop
- **ASP.NET Core Minimal APIs** — serves the UI (listings / watches / status / poll)
- **EF Core + SQLite** — content-signature dedup store (migrations code-first)
- **Playwright for .NET** — Marketplace scraping via CDP attach (no browser download)
- **Telegram bot** — alerts over a typed `HttpClient` with Polly resilience
- **React + TypeScript (Vite)** — the dashboard
- **xUnit** (TDD for parsing/filtering/dedup logic) · **GitHub Actions** CI

## Run it
```bash
# from the repo root
dotnet run --project src/MarketplaceWatcher
```
Configuration lives in `src/MarketplaceWatcher/appsettings.json` (watches, radius, poll interval),
with `appsettings.Development.json` overriding it in the Development environment. Secrets (e.g. the
Telegram bot token) are kept out of source via .NET user-secrets.

## Project layout
```
marketplace-watcher/
├─ MarketplaceWatcher.slnx          # solution file
├─ src/MarketplaceWatcher/          # the .NET service (worker + API)
│  ├─ Program.cs                    # host startup + service wiring
│  ├─ Worker/                       # background polling loop
│  ├─ Scraping/                     # CDP connection, parsing, text reconstruction
│  ├─ Data/                         # EF Core dbcontext + dedup store
│  ├─ Notifications/                # Telegram notifier
│  └─ Api/                          # minimal-API endpoints for the UI
├─ web/                             # React + TypeScript dashboard
└─ tests/                           # xUnit tests
```
