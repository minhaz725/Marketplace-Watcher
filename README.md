# Marketplace Watcher

A small personal tool that watches **Facebook Marketplace** for new listings I care about
(free stuff nearby, plus keyword searches like *sofa* or *iPhone 16 Pro Max*), filters out
the noise, and sends me a **Telegram** alert only for genuinely new items.

It's a **.NET 10 (C#) + React** port of an earlier Java/Spring project — built as a learning
project and portfolio piece.

## Tech stack
- **.NET 10** Worker Service (background polling loop via `BackgroundService`)
- **ASP.NET Core Minimal APIs** — serves a small web UI *(later)*
- **EF Core + SQLite** — remembers what's already been seen (dedup) *(later)*
- **Playwright for .NET** — reads Marketplace by attaching to a Chrome you already run *(later)*
- **Telegram bot** — the alerts *(later)*
- **React + TypeScript (Vite)** — a small dashboard UI *(later)*

## Status
Early stage — being built phase by phase.

- ✅ **Phase 0** — toolchain + git set up
- ✅ **Phase 1** — app skeleton: a worker that logs a heartbeat and reads layered JSON config
- ⏭️ **Phase 2 (next)** — strongly-typed config (Options pattern) + secrets

See **[PROGRESS.md](PROGRESS.md)** for detailed status and **[CLAUDE.md](CLAUDE.md)** for the full plan.

## Requirements
- [.NET SDK 10](https://dotnet.microsoft.com/) (`dotnet --version` → `10.x`)
- macOS/Linux/Windows

## Run it
```bash
# from the repo root
dotnet run --project src/MarketplaceWatcher
```
You'll see a heartbeat log every couple of seconds. Press **Ctrl-C** to stop (it shuts down cleanly).

Config lives in `src/MarketplaceWatcher/appsettings.json`, with
`appsettings.Development.json` overriding it when running in the Development environment.

## Project layout
```
marketplace-watcher/
├─ MarketplaceWatcher.slnx          # solution file
└─ src/MarketplaceWatcher/          # the worker app
   ├─ Program.cs                    # app startup + service wiring
   ├─ Worker/PollingWorker.cs       # the background loop
   └─ appsettings*.json             # configuration
```

> **Note:** personal-use tool. It watches a *secondary* account, reads gently, and never logs in
> for you — it attaches to a Chrome you start yourself.
