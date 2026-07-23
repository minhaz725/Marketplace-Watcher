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

### Phase 3 — Minimal web API
- `dotnet new worker` uses `Sdk="Microsoft.NET.Sdk.Worker"`, which does **not** pull in ASP.NET Core by default. Added `<FrameworkReference Include="Microsoft.AspNetCore.App" />` to the `.csproj` to make it available (this also made the earlier explicit `Microsoft.Extensions.Hosting` `PackageReference` redundant — removed it, since ASP.NET Core already brings it in transitively).
- `Program.cs` switched from `Host.CreateApplicationBuilder` to `WebApplication.CreateBuilder` — `WebApplication` still implements `IHost`, so `PollingWorker`'s hosted-service registration/lifecycle needed no changes.
- Gotcha (worth remembering): `Sdk.Worker`'s implicit-usings list doesn't include ASP.NET Core namespaces even with the `FrameworkReference` added — needed explicit `using Microsoft.AspNetCore.Builder;` in both `Program.cs` and `Api/Endpoints.cs` (the `MapGet` extension for Minimal APIs specifically lives in `.Builder`, not `.Routing`).
- Added `Api/Endpoints.cs`: a `MapWatcherEndpoints(this IEndpointRouteBuilder app)` extension method, called once from `Program.cs`, holding a `GET /health` → `200 {"status":"healthy"}`. This is where Phase 13's `/api/listings`, `/api/watches`, `/api/status`, `/api/poll` will be added.
- ▶ Verify passed: `dotnet run`, `curl localhost:5000/health` → `200 {"status":"healthy"}`, while the `PollingWorker` heartbeat kept logging in the same process.

### Commits (author = minhaz <minhaz725@gmail.com>, no co-author)
```
408299b feat: add Minimal API /health endpoint alongside the worker
85023a1 docs: record Phase 2 completion, point handover at Phase 3
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
   ├─ MarketplaceWatcher.csproj        # net10.0; Nullable + ImplicitUsings on; <UserSecretsId>; FrameworkReference Microsoft.AspNetCore.App
   ├─ Program.cs                       # WebApplication host; binds WatcherOptions; registers PollingWorker; maps API routes
   ├─ appsettings.json                 # base config — real Watcher section (PollIntervalSeconds, Location, Watches)
   ├─ appsettings.Development.json      # dev override (Logging only; Banner demo retired)
   ├─ Properties/launchSettings.json    # dev-only launch profile (sets DOTNET_ENVIRONMENT=Development)
   ├─ Config/WatcherOptions.cs          # WatcherOptions / Watch / GeoPoint — Options pattern POCOs
   ├─ Api/Endpoints.cs                 # MapWatcherEndpoints extension — GET /health
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

## ▶ NEXT: Phase 4 — EF Core + SQLite dedup store `[core]` (test-first)
Goal: `WatcherDbContext` with a `Seen` table keyed on a **content `Signature`** (not URL/listing-id — CLAUDE.md §8), a first EF Core migration, and a `GetNewAsync` that filters out already-seen listings. Write the xUnit test for the dedup logic first (TDD, per CLAUDE.md §8/§11).

Concrete steps (CLAUDE.md §9 Phase 4, §7 target structure):
1. Add the `tests/MarketplaceWatcher.Tests` xUnit project (doesn't exist yet) + reference it from the solution.
2. Add `Domain/Listing.cs` — a `record` with the fields dedup needs, and a `Signature` (normalized content hash of title+price+location per CLAUDE.md §8) — write its test first.
3. Add `Microsoft.EntityFrameworkCore.Sqlite` + `Microsoft.EntityFrameworkCore.Design` package references.
4. Add `Data/WatcherDbContext.cs` — `DbSet<Seen>` (or similar), keyed on `Signature`.
5. `dotnet ef migrations add InitialCreate` — teach: EF **code-first** migrations vs Flyway's **SQL-first** migrations (contrast explicitly, CLAUDE.md §4/§13).
6. `GetNewAsync(IEnumerable<Listing>)` — given candidate listings, returns only those whose `Signature` isn't already in `Seen`, then records the new ones.
7. ▶ Verify (CLAUDE.md §9 Phase 4): running the same candidate batch twice → second run finds 0 new; dedup logic covered by a test.

Teaching reminders for Phase 4: it's `[core]` → smaller steps, extra care, test-first. Anchor to Spring Data JPA + Postgres/Flyway, but don't assume EF Core mechanics transfer 1:1 (DbContext lifetime/scoping is the next phase's gotcha, not this one — PollingWorker doesn't touch the DbContext yet).

## Roadmap after Phase 4 (one per session)
5 polling loop w/ scope · 6 Playwright CDP attach · 7 parse + 3 defenses (crux, test-first) · 8 wire pipeline · 9 Telegram · 10 all watches + radius · 11 polish/docs · 12 CI · 13 React UI · 14 Postgres/Aspire · 15 LLM enrichment.
