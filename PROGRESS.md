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

### Phase 6 — Playwright .NET: attach over CDP
- Added `Microsoft.Playwright` package (no `playwright install` needed — we only *attach* to a Chrome the user starts, never launch one).
- `Scraping/CdpConnection.cs`: `IAsyncDisposable` wrapper. `ConnectAsync` does `Playwright.CreateAsync()` → `playwright.Chromium.ConnectOverCDPAsync(cdpUrl)`; exposes `Context` = `_browser.Contexts[0]` (the existing logged-in context — never `NewContextAsync()`). `DisposeAsync` calls `_browser.CloseAsync()` — confirmed via Playwright docs this **disconnects** rather than quitting Chrome when the browser was obtained via CDP attach (important per CLAUDE.md §5 — never actually close the user's real browser).
- Temporary verification hook in `Program.cs`, gated behind `--test-cdp` CLI arg: connects, opens a new page in the existing context, navigates to `facebook.com/marketplace/`, dumps `page.InnerTextAsync("body")` (first 500 chars) to console, then returns (skips the web host / worker). Explicitly commented as Phase 6 scaffolding — will be replaced by the real per-watch pipeline in Phase 8.
- **Real-world friction (worth remembering):** the secondary-account Chrome must be started manually first with the exact CLAUDE.md §5 command (`--remote-debugging-port=9222 --user-data-dir="$HOME/.fb-secondary-chrome"`) and logged in *in that window* — pointing this at an already-running daily-driver Chrome (real personal account, many tabs) was raised and explicitly rejected: CDP with no auth exposes every open tab/cookie to any local process, and the isolated secondary-account profile exists specifically to keep blast radius near-zero. `curl http://localhost:9222/json/version` (and `/json/list`) are the quick sanity checks that Chrome is actually listening and what pages/tabs currently exist before trying to attach.
- ▶ Verify passed: a new tab opened in the user's real, running secondary-account Chrome window (not a separate Playwright-launched browser); console printed `Page text length: 1542` followed by real Marketplace page text (categories, location, nav) — confirms attach + read-only navigation works end-to-end.

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
│  ├─ Data/Seen.cs, WatcherDbContext.cs, WatchEntity.cs  # Seen (dedup) + Watches tables
│  ├─ Migrations/                      # EF Core code-first migrations (InitialCreate, AddWatches)
│  ├─ Api/Endpoints.cs, CreateWatchRequest.cs  # /health, /api/watches (GET/POST/DELETE)
│  ├─ Scraping/CdpConnection.cs        # ConnectOverCDPAsync wrapper; reuses existing browser Context[0]
│  ├─ Scraping/CardParser.cs           # parses aria-label → ScrapedCard(Title, Price, Location, ListingId)
│  ├─ Scraping/WatchMatcher.cs         # ScrapedCard vs WatchEntity: freeOnly/minPrice/maxPrice/keyword filters
│  ├─ Scraping/IMarketplaceScraper.cs, MarketplaceScraper.cs  # per-watch URL + scroll-harvest, singleton CDP connection
│  ├─ Worker/PollingWorker.cs          # BackgroundService jittered poll loop; reads active watches from DB
│  └─ Worker/PollSignal.cs             # Channel-based signal so API calls can wake the poll loop early
└─ tests/MarketplaceWatcher.Tests/
   ├─ Domain/ListingTests.cs           # Signature test-first (5 cases)
   ├─ Data/WatcherDbContextTests.cs    # GetNewAsync dedup test-first (3 cases, in-memory SQLite)
   ├─ Data/WatchEntityTests.cs         # string[] JSON round-trip, IsActive filtering
   ├─ Scraping/CardParserTests.cs      # aria-label parsing against real captured fixtures
   ├─ Scraping/WatchMatcherTests.cs    # filter matching (freeOnly/price/keywords), 9 cases
   └─ Worker/PollSignalTests.cs        # early-wake vs. full-delay behaviour
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

### Phase 7 (in progress) — Parse cards from real captured DOM `[core]` (test-first)
- Used the `--test-cdp` scaffold (extended with a `--dump-cards [--url=...]` flag in `Program.cs`) to capture real listing-card HTML from both `facebook.com/marketplace/category/free` and a "sofa" search — reconnaissance before writing any parsing code, since CLAUDE.md §12 flags current selectors as a known unknown.
- **Finding that changed the plan:** no CSS-shuffled decoy text on feed cards across 4 real captured cards (free + priced). CLAUDE.md's "decoy text" defense turned out to be from the user's House Hunt experience with **FB Groups**, not Marketplace — confirmed with the user rather than building `TextReconstructor` against a hypothetical. Decided to work from concrete evidence instead of the original phase description.
- **Discovery:** every card's `aria-label` attribute contains clean, non-shuffled, comma-separated `"{title}, NZ${price}, {location}, listing {id}"` — accessibility text can't be visually scrambled without breaking screen readers, so it's a far more robust extraction point than reconstructing visible spans.
- `Scraping/CardParser.cs`: `[GeneratedRegex]` (source-generated regex, compiled at build time — newer idiom than `Regex.Compiled`) parses `aria-label` into a `ScrapedCard(Title, Price, Location, ListingId)` record. Title is matched non-greedily up to the first `", NZ$"` so it doesn't break on locations like "Auckland, New Zealand" that themselves contain a comma. Test-first against the real captured fixtures (5 cases, including a real malformed seller title and a thousands-separator price).
- `Scraping/WatchMatcher.cs`: `Matches(ScrapedCard, WatchEntity)` applies `freeOnly`/`minPrice`/`maxPrice`/`mustIncludeKeywords`/`excludeKeywords` (CLAUDE.md §6), case-insensitive keyword matching via `string.Contains(..., StringComparison.OrdinalIgnoreCase)`. Test-first, 9 cases. `radiusKm` deliberately not applied yet — `ScrapedCard.Location` is a display string ("Auckland, New Zealand"), not coordinates; real distance filtering needs geocoding, which CLAUDE.md defers to Phase 10.
- **Still open for Phase 7:** virtualized-feed scrolling (harvest cards as they render while scrolling, rather than just the first screenful) — not yet built. Everything else (parse card → `ScrapedCard`, filter against `WatchEntity`) is done. 26/26 tests green.

### Design change — dynamic, DB-backed watches (subscription model)
Mid-Phase-7, the user asked to change how watches are managed: instead of a static `Watches` list in `appsettings.json` (bound once at startup, requires a restart to change), watches are now created/stopped at runtime via the API — closer to a subscription model. Discussed and confirmed with the user first (see plan file, since implemented): **kept scraping strictly sequential** (one shared loop, one page at a time) even though watches are now dynamic — spawning one worker per watch would violate CLAUDE.md §5's "gentle, one page at a time" rule, since it'd mean concurrent Playwright pages against one Chrome session. Config watches retired entirely (DB-only going forward), not seeded from appsettings.
- `Data/WatchEntity.cs`: EF Core entity (`Id`, `Name`, `FreeOnly`, `MinPrice`/`MaxPrice`, `RadiusKm`, `MustIncludeKeywords`/`ExcludeKeywords`, `Condition`, `Category`, `IsActive` — soft-disable via `DELETE /api/watches/{id}` rather than a hard delete, so `Seen` history tied to a watch isn't orphaned, `CreatedAt`).
- `WatcherDbContext`: new `DbSet<WatchEntity> Watches`. `string[]` fields need a **value converter** (`HasConversion`, JSON serialize/deserialize — SQLite has no native array column) plus a **value comparer** (`ValueComparer<string[]>`) so EF's change tracker can detect content changes on the array instead of just comparing object references. Gotcha hit: `HasConversion`/`ValueComparer` lambdas are compiled as **expression trees**, and C#'s `[]` collection-expression syntax isn't legal inside one — had to use `Array.Empty<string>()` instead.
- New migration `AddWatches` (`dotnet ef migrations add AddWatches`).
- `Config/WatcherOptions.cs`: removed `List<Watch> Watches` and the `Watch` class entirely (superseded by `WatchEntity`); `appsettings.json`'s `Watches` array removed. `PollIntervalSeconds`/`DefaultRadiusKm`/`Location` stay in `WatcherOptions` since those are app-wide, not per-watch.
- `Worker/PollSignal.cs`: a singleton wrapping a bounded `Channel<bool>` (capacity 1, `DropWrite` on full — only the *fact* that something changed matters, not how many times). `Trigger()` (called after `POST /api/watches` saves) and `WaitAsync(delay, token)` (races `Task.Delay` against the channel via `Task.WhenAny` — whichever completes first wins) let the API wake the poll loop immediately instead of waiting out the full jittered interval. This is the same mechanism CLAUDE.md Phase 13 had already planned for `POST /api/poll` — built once here, reusable there.
- `PollingWorker`: each iteration now queries `db.Watches.Where(w => w.IsActive)` from the scoped `DbContext` instead of reading a static `IOptions<WatcherOptions>` list — a direct extension of the Phase 5 scope-per-iteration pattern, not a new mechanic. Delay changed from `Task.Delay` to `pollSignal.WaitAsync(...)`.
- `Api/Endpoints.cs`: `GET /api/watches`, `POST /api/watches` (creates + triggers the signal), `DELETE /api/watches/{id}` (soft-disable). These use ASP.NET Core's automatic per-request scoped `DbContext` injection into the minimal-API delegate directly — no manual `IServiceScopeFactory` needed here, that gotcha was specific to the *singleton* `PollingWorker`.
- ▶ Verified end-to-end: ran the app, `POST /api/watches` with a sofa watch → log immediately showed `1 active watch(es): sofa` (no restart, no waiting for the jittered interval) → `GET /api/watches` round-tripped it correctly including the `ExcludeKeywords` array.
- New tests: `WatchEntityTests` (string-array JSON round-trip via a second `DbContext` sharing the same open connection, to force a real DB read rather than reading back the change tracker's in-memory copy; `IsActive` filtering), `PollSignalTests` (wakes early when triggered; still respects the delay when never triggered). 17/17 tests green.

### Phase 8 — Wire the real pipeline `[anchor]`
- `Scraping/IMarketplaceScraper.cs` + `MarketplaceScraper.cs`: `ScrapeAsync(WatchEntity)` builds a search URL (`freeOnly` → `/marketplace/category/free`, else `/marketplace/search?query={watch.Name}`), opens a page on the (lazily-connected, reused) `CdpConnection`, and does the virtualized-feed scroll-harvest CLAUDE.md §9 Phase 7 called for: `page.Mouse.WheelAsync` to trigger real scroll events (more reliable than `window.scrollBy` for waking Marketplace's infinite-scroll listener), harvest currently-rendered cards' `aria-label`s via `CardParser`, dedup by `ListingId` across scrolls (a card can still be on-screen across two consecutive harvests), repeat 5×.
- `MarketplaceScraper` registered as a **singleton** (`IMarketplaceScraper`) — connecting over CDP spins up a Playwright driver process, too expensive to redo every ~90s poll, so the same `CdpConnection` is reused across all future polls; only opens/closes a fresh page per watch.
- `PollingWorker`: the loop now actually does the real thing per active watch — `scraper.ScrapeAsync(watch)` → `WatchMatcher.Matches` filters the cards → maps matches to `Listing` → `db.GetNewAsync`. Deliberately still a `foreach` (sequential `await`, not `Task.WhenAll`/parallel) — CLAUDE.md §5's "one page at a time" rule applies just as much to N dynamically-created watches as it did to the original static config list.
- Removed the temporary `--test-cdp`/`--dump-cards` scaffolding from `Program.cs` (Phase 6/7 reconnaissance-only, as their own comments promised) now that the real pipeline replaces them.
- ▶ Verified end-to-end against the real secondary-account Chrome: created a "sofa" watch (`$0–$200`, excluding wanted/parts/cover-only) via the API → log showed `Scraping https://www.facebook.com/marketplace/search?query=sofa for watch sofa` → `polled, 12 new` → confirmed via `sqlite3 watcher.db` that real listing titles (including one with an emoji, confirming Unicode handled fine) landed in `Seen`. Created a second ("free stuff") watch to trigger an immediate re-poll via `PollSignal` and confirmed the pipeline runs per-watch, sequentially. Deleted (soft-disabled) the throwaway test watch afterward via `DELETE /api/watches/{id}`.

## Roadmap after Phase 8 (one per session)
9 Telegram · 10 radius (client-side distance filter — `ScrapedCard.Location` is currently a display string, not coordinates, so this needs geocoding) · 11 polish/docs · 12 CI · 13 React UI (watch CRUD UI now has a real API to call already) · 14 Postgres/Aspire · 15 LLM enrichment.
