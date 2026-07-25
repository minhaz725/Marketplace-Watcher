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

### Phase 4 — EF Core + SQLite dedup store (test-first)
- Added `tests/MarketplaceWatcher.Tests` (xUnit), wired into `MarketplaceWatcher.slnx`, project-referencing the main project — the JUnit-module equivalent.
- `Domain/Listing.cs` — a `record` with a `Signature` computed property: SHA-256 of normalized (lowercased/trimmed) `title|price|location`, **deliberately excluding `Url`** (CLAUDE.md §8 — dedup on content, never URL/listing-id). Written test-first (`ListingTests.cs`, 5 cases).
- Added `Microsoft.EntityFrameworkCore.Sqlite` + `.Design` packages. Also pinned `SQLitePCLRaw.bundle_e_sqlite3` to `3.0.4` — the version EF Core 10 pulls transitively (`2.1.11`) has a known high-severity NuGet advisory (GHSA-2m69-gcr7-jv3q); the pin flows to the test project automatically via its project reference.
- `Data/Seen.cs` (entity) + `Data/WatcherDbContext.cs`: `DbSet<Seen>` keyed on `Signature` via Fluent API (`HasKey`); `GetNewAsync(IEnumerable<Listing>)` queries which candidate signatures are already in `Seen` (`.Contains` → SQL `IN`), returns the unseen ones, and records them in the same call (`AddRange` + `SaveChangesAsync`). Written test-first against an in-memory SQLite connection (`WatcherDbContextTests.cs`, 3 cases — including a mixed seen/new batch).
- First migration: `dotnet ef migrations add InitialCreate` (needed the `dotnet-ef` global tool installed) — contrasted with Flyway: EF generates the `Up`/`Down` SQL from a diff against `WatcherDbContextModelSnapshot.cs` (the current-model bookkeeping file), rather than you hand-writing SQL that Flyway applies directly.
- `appsettings.json` gained `ConnectionStrings:Watcher` = `"Data Source=watcher.db"`; `Program.cs` registers `WatcherDbContext` via `AddDbContext` (**scoped** — noted gotcha for Phase 5, since `PollingWorker` is a singleton) and calls `Database.Migrate()` inside a manually-created DI scope on startup.
- `.gitignore`: added `*.db`/`*.db-shm`/`*.db-wal` (SQLite runtime files, not source).
- ▶ Verify passed: 8/8 xUnit tests green (including "run twice → second call returns 0 new"); `dotnet run` against the real SQLite file applies the migration and creates the `Seen` table (confirmed via EF's SQL command logging).

### Phase 5 — BackgroundService polling loop
- `PollingWorker`'s fixed 2s heartbeat delay replaced with a jittered delay: `Random.Shared.Next(baseInterval - 30, baseInterval + 30)` seconds, where `baseInterval` = `WatcherOptions.PollIntervalSeconds` (90 by default → 60–120s range, matching CLAUDE.md §5).
- **The core concept:** `WatcherDbContext` is registered **Scoped** (Phase 4), but `PollingWorker` is a **Singleton** (`AddHostedService`). Injecting `WatcherDbContext` directly into `PollingWorker`'s constructor throws `InvalidOperationException: Cannot consume scoped service 'WatcherDbContext' from singleton 'PollingWorker'` at startup — .NET refuses to let a whole-app-lifetime object hold a should-be-short-lived unit-of-work. Fix: inject `IServiceScopeFactory` instead (itself singleton-safe — it's just a factory), and each loop iteration does `using var scope = scopeFactory.CreateScope()` → `scope.ServiceProvider.GetRequiredService<WatcherDbContext>()` → use it → dispose at the end of the `using` block. Anchor: same reasoning as why Spring scopes `EntityManager` per-request/per-transaction instead of wiring one as a singleton bean — the underlying DB *connection* is pooled/reused either way (analogous to a singleton `DataSource`), but the `DbContext`/`EntityManager` on top keeps an identity map that grows unbounded and can serve stale cached reads if kept alive forever.
- Each iteration now calls `db.GetNewAsync(Enumerable.Empty<Listing>(), stoppingToken)` (placeholder empty candidate list — real scraped candidates arrive in Phase 8) and logs `"polled, {Count} new"`.
- Needed an explicit `using Microsoft.Extensions.DependencyInjection;` in `PollingWorker.cs` for `IServiceScopeFactory`/`GetRequiredService` — same "Worker SDK's implicit-usings don't cover this namespace" gotcha as the ASP.NET Core `.Builder` one from Phase 3.
- ▶ Verify passed: EF Core's SQL command logging shows a real `SELECT "s"."Signature" FROM "Seen" AS "s" WHERE 0` per iteration (the `WHERE 0` is EF's translation of "empty candidate list → no signatures to match"), followed by `polled, 0 new`, repeating on the jittered interval; clean Ctrl-C shutdown confirmed.

### Commits (author = minhaz <minhaz725@gmail.com>, no co-author)
```
b421199 chore: ignore local SQLite data files
ef6605d feat: add EF Core SQLite dedup store
3e34b7b feat: add Listing domain record with content-based signature
c6e30f9 test: scaffold xUnit test project
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
├─ MarketplaceWatcher.slnx            # solution (root); references both src and tests projects
├─ src/MarketplaceWatcher/
│  ├─ MarketplaceWatcher.csproj        # net10.0; Nullable + ImplicitUsings on; <UserSecretsId>; FrameworkReference Microsoft.AspNetCore.App; EF Core Sqlite/.Design
│  ├─ Program.cs                       # WebApplication host; binds WatcherOptions; registers WatcherDbContext + PollingWorker; migrates DB; maps API routes
│  ├─ appsettings.json                 # base config — Watcher section + ConnectionStrings:Watcher
│  ├─ appsettings.Development.json      # dev override (Logging only; Banner demo retired)
│  ├─ Properties/launchSettings.json    # dev-only launch profile (sets DOTNET_ENVIRONMENT=Development)
│  ├─ Config/WatcherOptions.cs          # WatcherOptions / Watch / GeoPoint — Options pattern POCOs
│  ├─ Domain/Listing.cs                # record with content Signature (SHA-256, excludes Url)
│  ├─ Data/Seen.cs, WatcherDbContext.cs # Seen table keyed on Signature; GetNewAsync dedup query
│  ├─ Migrations/                      # EF Core code-first migrations (InitialCreate)
│  ├─ Api/Endpoints.cs                 # MapWatcherEndpoints extension — GET /health
│  └─ Worker/PollingWorker.cs          # BackgroundService jittered poll loop; scoped WatcherDbContext via IServiceScopeFactory
└─ tests/MarketplaceWatcher.Tests/
   ├─ Domain/ListingTests.cs           # Signature test-first (5 cases)
   └─ Data/WatcherDbContextTests.cs    # GetNewAsync dedup test-first (3 cases, in-memory SQLite)
```
(User-secrets store lives outside the repo, keyed by `<UserSecretsId>` in the `.csproj`; holds `Telegram:BotToken`. `watcher.db` is a runtime artifact, gitignored.)

## How to run / verify quickly
```bash
dotnet build
dotnet test tests/MarketplaceWatcher.Tests               # xUnit — pure logic + dedup, test-first
dotnet run --project src/MarketplaceWatcher               # Development (uses launchSettings) → migrates DB, heartbeat, /health
```
Note: a plain `.NET` console app ignores SIGPIPE, so piping to `head` won't stop it — stop with Ctrl-C (or run in background and kill).
`dotnet-ef` is a **global tool** (`dotnet tool install --global dotnet-ef`); its install path (`~/.dotnet/tools`) may need adding to `PATH` for a new shell.

---

## ▶ NEXT: Phase 6 — Playwright .NET: attach over CDP `[core]`
Goal: add `Microsoft.Playwright`, write a `CdpConnection` that attaches to *your* already-running, already-logged-in secondary-account Chrome (CLAUDE.md §5 — `--remote-debugging-port=9222`), grabs the first existing browser context (never creates a new one, never logs in), opens a page, navigates to a Marketplace search URL, and dumps `innerText` so we can see raw scraped text end-to-end before Phase 7 does real parsing.

Concrete steps (CLAUDE.md §9 Phase 6):
1. Add the `Microsoft.Playwright` NuGet package to the main project — no browser download needed (`playwright install` is for *launching* browsers; we're only *attaching* to one you start yourself).
2. Before running anything, start the secondary-account Chrome per CLAUDE.md §5 (`--remote-debugging-port=9222 --user-data-dir=...`), logged into the secondary FB account.
3. `Scraping/CdpConnection.cs`: `Playwright.CreateAsync()` → `playwright.Chromium.ConnectOverCDPAsync("http://localhost:9222")` → `browser.Contexts[0]` (reuse the existing logged-in context — **never** `NewContextAsync()`) → `context.NewPageAsync()`.
4. Navigate to a Marketplace search URL (e.g. free-near-me), then read `page.InnerTextAsync("body")` (or similar) and log/dump it.
5. ▶ Verify (CLAUDE.md §9 Phase 6): a page in *your* logged-in Chrome actually loads (visible in the Chrome window you started); we print its text to the console.

Teaching reminders for Phase 6: it's `[core]` — new material (Playwright, CDP attach) even though the *shape* anchors to your Java `connectOverCDP()` + `contexts().get(0)` pattern from House Hunt. Go slow on: what CDP is, why "attach" (not "launch") matters for using a real logged-in session safely, and the .NET async Playwright API mechanics (`IPlaywright`, `IBrowser`, `IBrowserContext`, `IPage`).

## Roadmap after Phase 6 (one per session)
7 parse + 3 defenses (crux, test-first) · 8 wire pipeline · 9 Telegram · 10 all watches + radius · 11 polish/docs · 12 CI · 13 React UI · 14 Postgres/Aspire · 15 LLM enrichment.
