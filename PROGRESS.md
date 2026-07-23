# PROGRESS — Marketplace Watcher

> Handover notes so a **new Claude Code session** can pick up cleanly.
> **Read `CLAUDE.md` first** — especially **§3 "How to teach me"** and **§9 (phased roadmap)**. This file only records where we are.

---

## Ground rules that carry across every session
- **Learning-first project.** The user is an experienced backend engineer (Java/Spring, Go) but a *genuine .NET learner*. Teach: anchor to Spring/House Hunt → then the .NET idiom → then the mechanics → then the code. Tiny steps, small diffs, pause at every ▶ Verify. **Frontend (React, Phase 13) is the weakest area — go slowest there.**
- **One phase per session.** Stop at the phase's ▶ Verify.
- **Git identity is repo-local:** commits are authored **`minhaz <minhaz725@gmail.com>`** (overrides the machine global, which is a work email). **Never add a `Co-Authored-By` trailer.**
- Trunk-based: small, logical, revertable commits.

---

## ✅ Done so far

### Phase 0 — Environment
- .NET SDK **10.0.302** installed via Homebrew cask (`brew install --cask dotnet-sdk`), linked at `/opt/homebrew/bin/dotnet`. `dotnet --version` → `10.0.302`.
- git initialized; **repo-local** identity set (`user.name minhaz`, `user.email minhaz725@gmail.com`). Global work email untouched.
- `.gitignore` generated via `dotnet new gitignore`.

### Phase 1 — Scaffold + config tour
- Created solution + worker project:
  - `dotnet new sln -n MarketplaceWatcher` → produced **`MarketplaceWatcher.slnx`** (⚠️ .NET 10 defaults to the new **XML `.slnx`** format, not classic `.sln`; CLAUDE.md §7 diagram says `.sln` but `.slnx` is fine — note for DECISIONS.md later).
  - `dotnet new worker -o src/MarketplaceWatcher` + `dotnet sln add`.
- Renamed generated `Worker` → **`PollingWorker`** at `src/MarketplaceWatcher/Worker/PollingWorker.cs` (namespace `MarketplaceWatcher.Worker`).
- `PollingWorker` currently: injects `ILogger` + `IConfiguration`, logs a **heartbeat every 2s**, and logs a `Watcher:Banner` value read via **raw `config["Watcher:Banner"]`**.
- Added `Watcher:Banner` to `appsettings.json` (base) and `appsettings.Development.json` (override) to **demonstrate config precedence**.
- ▶ Verify passed: heartbeat loops; Development banner overrides base; graceful shutdown on SIGINT (`Application is shutting down...`).

### Phase 2 — Generic Host: DI + Options + secrets
- Added `Config/WatcherOptions.cs`: `WatcherOptions` (`Section = "Watcher"`), `Watch`, `GeoPoint` — plain mutable POCOs (not records) since the config binder sets their properties via reflection.
- Bound in `Program.cs`: `builder.Services.Configure<WatcherOptions>(builder.Configuration.GetSection(WatcherOptions.Section))`.
- `PollingWorker` now injects `IOptions<WatcherOptions>` (retired the raw `config["Watcher:Banner"]` demo from Phase 1) and logs the bound watch list on startup.
- `appsettings.json` now has the real `Watcher` section (`PollIntervalSeconds`, `DefaultRadiusKm`, `Location`, and the free-near-me / sofa / iPhone 16 Pro Max watches from CLAUDE.md §6). `Watcher:Banner` removed from both `appsettings.json` and `appsettings.Development.json` — no longer used.
- Added a real secret: `dotnet user-secrets set "Telegram:BotToken" "dummy-for-now"` (secrets store lives outside the repo, keyed by the `<UserSecretsId>` already in the `.csproj`). `PollingWorker` reads it via `IConfiguration` (no Options class yet — single ad-hoc value) and logs only whether it loaded, never the value.
- ▶ Verify passed: startup log shows `PollingWorker starting. 3 watch(es) configured: (free-only / match-all), sofa, iPhone 16 Pro Max` and `Telegram bot token loaded from config: True`; heartbeat continues as before.

### Commits (author = minhaz <minhaz725@gmail.com>, no co-author)
```
cbbb316 chore: replace Banner demo config with real watch list
caf9852 feat: bind Watcher config via strongly-typed Options pattern
ac9b3f8 feat: add PollingWorker heartbeat with config-driven banner
c64cedf chore: scaffold .NET 10 worker service solution
```

---

## Current repo layout
```
marketplace-watcher/
├─ CLAUDE.md · PROGRESS.md · README.md
├─ MarketplaceWatcher.slnx            # solution (root)
└─ src/MarketplaceWatcher/
   ├─ MarketplaceWatcher.csproj        # net10.0; Nullable + ImplicitUsings on; <UserSecretsId> present
   ├─ Program.cs                       # Generic Host; binds WatcherOptions; registers PollingWorker
   ├─ appsettings.json                 # base config — real Watcher section (PollIntervalSeconds, Location, Watches)
   ├─ appsettings.Development.json      # dev override (Logging only; Banner demo retired)
   ├─ Properties/launchSettings.json    # dev-only launch profile (sets DOTNET_ENVIRONMENT=Development)
   ├─ Config/WatcherOptions.cs          # WatcherOptions / Watch / GeoPoint — Options pattern POCOs
   └─ Worker/PollingWorker.cs          # BackgroundService heartbeat; consumes IOptions<WatcherOptions>
```
(User-secrets store lives outside the repo, keyed by `<UserSecretsId>` in the `.csproj`; holds `Telegram:BotToken`.)

## How to run / verify quickly
```bash
dotnet build src/MarketplaceWatcher
dotnet run --project src/MarketplaceWatcher        # Development (uses launchSettings) → DEV banner + heartbeat
```
Note: a plain `.NET` console app ignores SIGPIPE, so piping to `head` won't stop it — stop with Ctrl-C (or run in background and kill).

---

## ▶ NEXT: Phase 3 — Minimal web API `[anchor]`
Goal: stand up the ASP.NET Core Minimal API host alongside the worker, starting with a single `/health` endpoint. This is the API that grows in Phase 13 to serve the React UI (listings/watches/status/poll), so it's worth getting the hosting model right now even though today's payload is trivial.

Concrete steps (CLAUDE.md §9 Phase 3, §4 web API row):
1. Decide/confirm hosting shape: `WebApplication.CreateBuilder` in `Program.cs` alongside the existing worker registration (Generic Host can host both a `BackgroundService` and Minimal API endpoints in one process — teach why this differs from a separate Spring `@RestController` app).
2. Add `Api/Endpoints.cs` with a `MapHealthEndpoints` (or similar) extension method; wire it from `Program.cs` — teach the extension-method-on-`WebApplication`/`IEndpointRouteBuilder` idiom, since it's how Minimal API routes stay organized instead of one giant `Program.cs`.
3. Add `GET /health` returning 200 (+ maybe a trivial JSON body).
4. ▶ Verify (CLAUDE.md §9 Phase 3): `dotnet run`, then `curl localhost:<port>/health` → 200, while the worker's heartbeat keeps logging in the same process.

Teaching reminders for Phase 3: it's `[anchor]` — motivate fast from the Spring MVC controller analogy, but still cover the .NET-specific mechanics (Minimal API routing, why no controller classes needed, `IEndpointRouteBuilder`).

## Roadmap after Phase 3 (one per session)
4 EF Core + SQLite dedup (test-first) · 5 polling loop w/ scope · 6 Playwright CDP attach · 7 parse + 3 defenses (crux, test-first) · 8 wire pipeline · 9 Telegram · 10 all watches + radius · 11 polish/docs · 12 CI · 13 React UI · 14 Postgres/Aspire · 15 LLM enrichment.
