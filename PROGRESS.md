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
│  ├─ Config/WatcherOptions.cs          # WatcherOptions / GeoPoint — Options pattern POCOs
│  ├─ Config/TelegramOptions.cs         # BotToken + ChatId, [Required], ValidateOnStart
│  ├─ Notifications/INotifier.cs, TelegramNotifier.cs  # typed HttpClient + Polly resilience
│  ├─ Domain/Listing.cs                # record with content Signature (SHA-256, excludes Url)
│  ├─ Data/Seen.cs, WatcherDbContext.cs, WatchEntity.cs  # Seen (dedup) + Watches tables
│  ├─ Migrations/                      # EF Core code-first migrations (InitialCreate, AddWatches)
│  ├─ Api/Endpoints.cs, CreateWatchRequest.cs  # /health, /api/watches (GET/POST/DELETE)
│  ├─ Scraping/CdpConnection.cs        # ConnectOverCDPAsync wrapper; reuses existing browser Context[0]
│  ├─ Scraping/CardParser.cs           # parses aria-label → ScrapedCard(Title, Price, Location, ListingId)
│  ├─ Scraping/WatchMatcher.cs         # ScrapedCard vs WatchEntity: freeOnly/minPrice/maxPrice/keyword filters
│  ├─ Scraping/IMarketplaceScraper.cs, MarketplaceScraper.cs  # per-watch URL + scroll-harvest, singleton CDP connection
│  ├─ Scraping/HarvestPolicy.cs        # pure stop-condition for the scroll-harvest (+ truncation detection)
│  ├─ Worker/PollingWorker.cs          # BackgroundService jittered poll loop; reads active watches from DB
│  └─ Worker/PollSignal.cs             # Channel-based signal so API calls can wake the poll loop early
└─ tests/MarketplaceWatcher.Tests/
   ├─ Domain/ListingTests.cs           # Signature test-first (5 cases)
   ├─ Data/WatcherDbContextTests.cs    # GetNewAsync dedup test-first (3 cases, in-memory SQLite)
   ├─ Data/WatchEntityTests.cs         # string[] JSON round-trip, IsActive filtering
   ├─ Scraping/CardParserTests.cs      # aria-label parsing against real captured fixtures
   ├─ Scraping/WatchMatcherTests.cs    # filter matching (freeOnly/price/keywords), 9 cases
   ├─ Scraping/HarvestPolicyTests.cs   # scroll stop-condition + truncation detection, 9 cases
   └─ Worker/PollSignalTests.cs        # early-wake vs. full-delay behaviour
```
(User-secrets store lives outside the repo, keyed by `<UserSecretsId>` in the `.csproj`; holds `Telegram:BotToken` + `Telegram:ChatId`. `watcher.db` is a runtime artifact, gitignored.)

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

### Interim — MATCHED log line (ahead of Phase 9)
No `INotifier`/Telegram integration exists yet, so a genuine match was previously invisible beyond the aggregate `polled, {N} new` count. Added a per-listing log line in `PollingWorker` — `logger.LogInformation("MATCHED [{Watch}]: {Title} (${Price}) - {Url}", ...)` — logged once per genuinely-new listing (post-`GetNewAsync`, so only real matches, not reposts/already-seen). This is a stopgap for visibility, not a replacement for Phase 9 — it'll likely stay alongside the eventual Telegram send, not get removed by it.

### Phase 9 — Telegram notifier `[anchor]`
- **Decided against WhatsApp first.** Asked before starting whether WhatsApp could replace Telegram. It can't usefully: the only sanctioned route is the WhatsApp Business Cloud API (Meta business account + a *dedicated* phone number + business verification), and an alert bot is **business-initiated** messaging — the paid category, charged per message from the first one, and restricted to pre-approved templates rather than free-form text. The only free path (the 24h "customer service window" opened by the user messaging first) requires manually poking your own bot daily *and* Meta begins charging for in-window non-template replies from **1 Oct 2026**. Unofficial libs (Baileys / whatsapp-web.js) violate ToS and risk a number ban. Telegram: free, unlimited, 60 seconds via @BotFather. **Write this up as an ADR in Phase 11** — it's a genuine "clear technical options" signal (CLAUDE.md §11).
- Secrets: `Telegram:BotToken` (the Phase 2 `"dummy-for-now"` placeholder replaced with a real token) + new `Telegram:ChatId`. **Gotcha:** `dotnet user-secrets` resolves the project from the *current directory* — running it from the repo root fails with "Could not find a MSBuild project file" because the root only has `MarketplaceWatcher.slnx`, and a solution carries no `<UserSecretsId>`. Use `--project src/MarketplaceWatcher` or `cd` in first.
- `Config/TelegramOptions.cs`: `BotToken` + `ChatId`, both `[Required]`. `ChatId` is a **`string`, not a `long`** — channel ids are big negatives (`-100…`) but the API also accepts `"@channelname"`.
- Registered with `AddOptions<T>().Bind(...).ValidateDataAnnotations().ValidateOnStart()` — a deliberate **upgrade over Phase 2's plain `Configure<T>()`**, which binds lazily and would only surface a missing token at first use. `ValidateOnStart` turns it into a loud crash at boot.
- `Notifications/INotifier.cs` + `TelegramNotifier.cs`: typed `HttpClient` via `AddHttpClient<INotifier, TelegramNotifier>`, `BaseAddress = https://api.telegram.org/bot{token}/`. **Trailing slash is load-bearing** — `Uri` combination follows browser rules, so without it the relative `"sendMessage"` *replaces* the last path segment instead of appending.
- Resilience: `Microsoft.Extensions.Http.Resilience` + `.AddStandardResilienceHandler()` (retry w/ exponential backoff + jitter, circuit breaker, timeouts). This is Microsoft's first-party wrapper over **Polly v8** and supersedes the older `Microsoft.Extensions.Http.Polly` package most blog posts still show.
- Message uses `parse_mode: HTML` with every interpolated value `WebUtility.HtmlEncode`d — a listing title containing a bare `<` or `&` would otherwise make Telegram reject the whole message. New C# mechanics used: `[property: JsonPropertyName("chat_id")]` (the `property:` target is *required* on a positional record parameter, else the attribute is ambiguous) and `"""` raw string literals (closing-quote indentation is stripped from every line).
- Errors are swallowed and logged, never thrown — a throwing notifier would kill `ExecuteAsync` and stop the `BackgroundService` permanently. An **exception filter** (`catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }`) separates genuine Ctrl-C shutdown from a real send failure. Non-2xx responses log the **body**, since Telegram puts the actual reason ("chat not found", "bot was blocked") there rather than in the status code.
- **`INotifier` is resolved from the per-iteration scope, not injected into `PollingWorker`'s constructor.** Sharper cousin of the Phase 5 gotcha: typed HTTP clients are **transient**, and transient-into-singleton — unlike scoped-into-singleton — does **not** throw. It silently creates a captive dependency where the singleton pins one `HttpClient` for the app's lifetime, defeating exactly the handler rotation `IHttpClientFactory` exists to provide.
- Removed `PollingWorker`'s now-dead `IConfiguration config` parameter and its Phase 2 `"Telegram bot token loaded: True"` line (superseded by validated Options). The `MATCHED` log line stays alongside the Telegram send, as the interim note predicted.
- Debugging ladder worth reusing: prove credentials with a raw `curl -s "https://api.telegram.org/bot<TOKEN>/sendMessage" -d chat_id=<ID> -d text=hi` → `{"ok":true}` *before* running the app. If it says `chat not found`, the bot needs adding to the channel as an **administrator** with "Post Messages".
- ⚠️ **Known tradeoff — at-most-once delivery (deferred deliberately).** `GetNewAsync` marks listings seen *and* commits in the same call, so a listing is already in `Seen` before `NotifyAsync` runs. If Telegram is down and retries exhaust, that alert is **lost permanently**. Fixing it properly means splitting `GetNewAsync` into "which are new?" + "mark these sent" — a real change to the Phase 4 data layer, so it was left out rather than smuggled into Phase 9. User chose "note it and move on". **Record as an ADR in Phase 11**; revisit if it bites in practice.
- ▶ Verify: pending — build clean, 26/26 tests green, but the end-to-end "real listing pings my phone" check still needs the secondary-account Chrome on port 9222 and a live poll.

### Phase 10 — retargeted from "radius" to harvest reliability `[core]` (test-first)
Started as the radius/distance-filter phase. Reconnaissance killed that plan and surfaced a real bug instead, so the phase was **retargeted with the user's agreement** rather than shipping a filter that provably does nothing.

**Reconnaissance (temporary `Scraping/LocationRecon.cs` + `--recon` flag, both deleted afterward — same throwaway-scaffold pattern as Phases 6/7).**
- **Q1 — is `Location` ever finer than city?** Across 161 harvested cards: **154 `"Auckland, New Zealand"`, 7 `"Takanini, New Zealand"`**. So it *can* be suburb-level (correcting the earlier "always city" assumption from the 4 Phase 7 fixtures), but at ~96% city-level, geocoding + haversine could discriminate roughly 1 listing in 25. Not worth the machinery.
- **Q2 — do `latitude`/`longitude`/`radius` URL params work?** **No.** Facebook ignores them; radius comes from the *account's* saved Marketplace location.
- ⚠️ **Methodology lesson worth keeping.** The first version of the recon compared narrow-radius vs wide-radius result sets and concluded "they differ → the param works." **That was a false positive.** Adding a control — fetching the *same* URL twice to establish a noise floor — showed same-URL similarity of **75%** and narrow-vs-wide similarity of **75%**: identical, i.e. zero effect. Any time you test a system you don't control and whose output is nondeterministic, **measure it against itself first**, then check whether your signal exceeds that noise. Difference alone proves nothing.
- User confirmed Facebook's location picker only offers **cities**, not suburbs — so re-centring the circle on Wesley/Mt Roskill isn't available either. Radius is environmental, full stop.

**Decision (option C): `RadiusKm` is deliberately NOT applied.** Documented in a comment on `WatchMatcher.Matches` explaining *why*, so a future reader doesn't "fix" the apparent omission. `RadiusKm` stays on `WatchEntity` (harmless, and the UI may surface it later). **ADR for Phase 11.**

**The real bug the recon exposed.** The same URL yielded **23 listings on one fetch and 46 on the next** — meaning `MarketplaceScraper` had been silently harvesting as little as half the feed since Phase 8. A listing never harvested is an alert never sent. Two causes, two fixes:
- **`GotoAsync` returning ≠ cards rendered.** Marketplace builds its grid client-side, so the first harvest often ran against an empty/partial DOM. Now waits on `page.WaitForSelectorAsync(CardSelector, Timeout = 20s)` first, and returns empty with a `LogWarning` if nothing ever appears.
- **A fixed 5 scroll rounds could stop mid-feed.** Replaced with `Scraping/HarvestPolicy.cs` — pure, testable logic (`maxRounds: 12`, `stagnantRoundsBeforeStop: 2`) that keeps scrolling until **two consecutive rounds** yield no unseen `ListingId`. Two, not one, specifically because a single slow lazy-load round must not be mistaken for "feed exhausted" — that misreading *was* the bug.
- **No silent caps.** `HarvestPolicy.StoppedEarly(...)` distinguishes "stopped because the feed ran dry" from "stopped because the round cap ran out", and the latter logs a `LogWarning` naming the missed listings. A truncated harvest otherwise looks identical to a complete one in the logs.
- Written test-first (`HarvestPolicyTests.cs`, 9 cases incl. the argument-validation `[Theory]`). **35/35 tests green.**

▶ **Verify passed — the point of the phase.** Two consecutive live runs produced **byte-identical** harvests: `macbook pro` 141 listings / 12 rounds and `Samsung S26 Ultra` 49 / 6 rounds, both times — against a pre-fix baseline that swung 23↔46 on the same URL. Zero truncation warnings fired, so 141 was genuine feed exhaustion that happened to use all 12 rounds (an earlier reading of the round count as "truncated" was wrong; the detector disproved it). Second run logged `polled, 0 new` — dedup holding, no duplicate Telegram traffic. Phase 9's notifier also confirmed working end-to-end here: 20 real alerts delivered on run 1 with **0** send failures.

**Follow-up worth considering (not done):** Marketplace supports a `sortBy=creation_time_descending` URL param. If it works — and unlike `radius` it's a real UI feature, so test it the same controlled way — newest listings would sort to the top, and the deep 12-round scroll every ~90s could shrink to 2–3 rounds. That's both gentler (CLAUDE.md §5) and faster.

### Bugfix — intra-batch signature collisions crashed the whole host
Surfaced immediately after Phase 10 by a new **"Full Mirror"** watch: `InvalidOperationException: The instance of entity type 'Seen' cannot be tracked because another instance with the same key value for {'Signature'} is already being tracked`.
- **Cause:** `GetNewAsync` deduped candidates against the **database** but not against **each other**. `Signature` deliberately excludes `Url` (CLAUDE.md §8), so several genuinely-distinct Marketplace listings sharing a title/price/location ("Full Mirror", $40, "Auckland, New Zealand") collapse to one primary key — and `AddRange` was handed two entities with the same key.
- **Latent since Phase 4**, but only triggered now: the "Full Mirror" search has unusually repetitive titles, *and* Phase 10's harvest fix tripled batch sizes (49 vs ~20), so collisions became likely rather than rare.
- **Fix:** one line — `candidates.DistinctBy(c => c.Signature)` before querying/adding. Semantically right, not just a crash patch: by our own dedup definition two identical-content listings *are* the same item, so returning both would also have sent duplicate Telegram alerts.
- Test-first: reproduced the exact exception in a unit test (three same-signature listings in one batch) before fixing — no Facebook needed. Plus a second case covering intra-batch duplicates where one was already seen. **37/37 green.**
- ▶ Verified live against the offending watch: `Harvested 49 listings in 7 scroll round(s) for watch Full Mirror` → `polled, 14 new`, 14 alerts delivered, no crash.
- ⚠️ **Follow-up for Phase 11:** note *how* this failed — one exception in `ExecuteAsync` took down the **entire host** (`BackgroundServiceExceptionBehavior.StopHost` is the default). The watcher didn't degrade, it died. Phase 11's "error backoff" item should wrap the per-watch work in a try/catch so one bad watch skips its turn instead of stopping the app.

### Location filter — out-of-region listings (corrects a Phase 10 conclusion)
User reported Telegram alerts arriving from **Tauranga, Hamilton and Napier** despite an Auckland account.
- **Phase 10's conclusion was too broad and is corrected here.** The recon sampled only a `sofa` search — a query with dense Auckland supply — saw 96% `"Auckland, New Zealand"`, and concluded location strings carried no usable signal. Wrong. Facebook treats the account location as a **preference, not a boundary**: when a search has few local results it pads the feed with nationwide listings. A sparse query (`macbook`, `Full Mirror`) exposes what a dense one hides. *Lesson: one query shape is not a sample.* (The two well-controlled Phase 10 findings still stand — `radius` URL params are genuinely ignored, and account location genuinely drives the default result set.)
- **Also corrected: geocoding is unnecessary.** The initial proposal was a geocode + cache + haversine phase. The user pointed out the card already carries the location and we already parse it into `ScrapedCard.Location` — filtering the string we have needs no network calls, no new table, no migration. The simpler design was the right one.
- `Scraping/LocationFilter.cs`: `IsAllowed(location, allowedLocations)` — case-insensitive substring match against configured place names. **Empty list = filter disabled**, never "reject everything"; an unconfigured filter must not silently stop all alerts. Test-first, 9 cases.
- `WatcherOptions.AllowedLocations` (`string[]`, default empty) bound from `appsettings.json`, which ships `[ "Auckland" ]`. Applied in `PollingWorker` **before** `WatchMatcher`.
- **No silent drops.** When anything is filtered out, `PollingWorker` logs the count plus the *distinct rejected location strings*. This is load-bearing, not decoration — see below.
- ▶ Verified live on a `macbook` watch: `Harvested 142` → `Dropped 65 out-of-region listing(s)`. **46% of the feed was out-of-region.**
- ⚠️ **Known limitation the log immediately exposed.** The filter is *fail-closed* on suburbs: Auckland places whose names don't contain "Auckland" get dropped — the run rejected **Swanson, Belmont, Warkworth, Whangaparaoa, Stanmore Bay, Silverdale, Orewa, Puhoi, Matakana, Buckland** alongside the genuine out-of-region ones. Curate by adding wanted suburbs to `AllowedLocations` (config only, no code change). Note many of those are 40–75 km north of Wesley, so dropping them may be *desirable* — but it should be a choice, not an accident.

### Time filter — `daysSinceListed` (scrape only recent listings)
Question raised: can we fetch only recently-posted listings instead of the whole feed? Answered by reconnaissance (temporary `Scraping/TimeFilterRecon.cs` + `--recon-time`, deleted afterward; read-only — no `DbContext`, no `INotifier`, so no Telegram traffic during testing).
- **The card carries no timestamp.** `aria-label` is `{title}, NZ${price}, {location}, listing {id}` — nothing more. Reading posted-time from each listing's *detail page* was rejected outright: ~140 extra page loads per poll violates CLAUDE.md §5.
- **Discovery: a `"Just listed"` badge exists in the card's `innerText` but NOT its `aria-label`.** Recorded because it is the *only* time signal Marketplace exposes on a card — useful if the URL param ever stops working. Deliberately not captured into `ScrapedCard`: with `daysSinceListed` filtering server-side, a "fresh" flag on already-fresh listings earns nothing.
- **`sortBy=creation_time_descending` is IGNORED** — 100% set similarity *and* identical first-12 ordering across every comparison. Same as `radius`.
- **`daysSinceListed=1` WORKS.** Noise floor (same URL twice) 51%, baseline-vs-filtered signal 17% — well outside the noise. Corroborated independently: share of cards tagged `"Just listed"` rises from **16% (8/49) to ~77% (19/25, 38/49)**. Two unrelated metrics agreeing is what the original `radius` test lacked.
- `Scraping/SearchUrlBuilder.cs`: extracted `BuildSearchUrl` out of `MarketplaceScraper` into pure, unit-tested logic (7 cases — `?` vs `&` separator for the free-category URL, keyword escaping, and omitting a nonsensical `daysSinceListed=0`). `MarketplaceScraper` now injects `IOptions<WatcherOptions>`.
- `WatcherOptions.DaysSinceListed` (`int?`, default **1**) in `appsettings.json`. Global rather than per-watch for now — per-watch would need a `WatchEntity` column + migration, and is easy to promote later.
- **Signature dedup deliberately kept.** The time filter narrows *what we look at*; dedup decides *what is genuinely new*. Different jobs — and if Facebook silently drops the param, dedup is the backstop.
- ▶ Verified live: `Scraping .../search?query=macbook&daysSinceListed=1` → `Harvested 55 listings in 7 scroll round(s)` (was **142 in 12**), `Dropped 17 out-of-region`, `polled, 17 new`. **61% fewer listings, 5 fewer rounds.** No truncation warning at 7 rounds, so the 24-hour pool is now provably covered within the cap — which closes the "a brand-new listing could sit past the round cap" correctness worry from Phase 10, by shrinking the pool rather than by sorting.

### Phase 11 (started) — delivery guarantee + host resilience
Two real defects fixed. User decided: **Telegram only** (no WhatsApp implementation), **skip the radius ADR**, and `AllowedLocations` suburb curation is theirs to edit as needed.

**Fix 1 — at-most-once → at-least-once delivery.** Previously `GetNewAsync` recorded listings in `Seen` *and* committed before the alert was attempted, so a Telegram outage lost alerts permanently (flagged in Phase 9, deferred then).
- `GetNewAsync` is now **read-only** — it answers "which of these are new?" and commits nothing.
- New `MarkSeenAsync(listings)` records them, and `PollingWorker` calls it **only with listings whose alert actually sent**.
- `INotifier.NotifyAsync` now returns `Task<bool>` instead of `Task`; `TelegramNotifier` returns false on both non-2xx and thrown failures. Still never throws — the contract is unchanged, it just reports now.
- Failed sends stay unseen and are retried next poll, with a `LogWarning` naming how many. **The tradeoff is now duplicates instead of losses** — if a send succeeds but the process dies before `MarkSeenAsync`, you get the alert twice. That is the right direction for an alerting tool.
- `MarkSeenAsync` re-checks the DB before inserting, since a signature can recur across watches within one poll and a duplicate key would throw.
- Note: `polled, {N} new` now counts **delivered** alerts, not found ones.

**Fix 2 — one exception no longer kills the app.** `BackgroundServiceExceptionBehavior.StopHost` is the .NET default, so any unhandled exception in `ExecuteAsync` stopped the whole watcher silently — exactly how the `Full Mirror` crash manifested.
- Per-watch `try/catch`: a broken watch (Playwright timeout, DOM change) logs and the loop continues to the next watch.
- Outer `try/catch` around the whole iteration for anything else (DB, scope creation).
- Both use the exception-filter idiom `catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)` so genuine Ctrl-C shutdown still unwinds cleanly instead of being swallowed as a failure.
- `Worker/PollBackoff.cs`: pure, test-first (8 cases). Consecutive failed polls double the interval — 1×, 2×, 4×, 8× — **capped at 8×** so a long outage still retries about every 12 minutes rather than drifting to hours. `int.MaxValue` is covered as a test case to guard the shift operator against overflow.
- ▶ Verified live (happy path): `Harvested 49 in 6 rounds`, `Dropped 5 out-of-region`, `polled, 5 new`, all delivered, no warnings. **64/64 tests green.**
- ⚠️ **Not yet live-tested: the failure path.** Backoff and the delivery split are covered by unit tests and by construction, but no induced live failure has been observed. The definitive check is to close the secondary-account Chrome mid-run and confirm the app logs `Watch ... failed this poll; continuing` and backs off, rather than exiting.

## ▶ REMAINING in Phase 11
Error backoff ✅ done above. Still to do: structured logging (optionally Serilog), `/status` endpoint, README run steps, on-call runbook, and **DECISIONS.md** with two ADRs — (a) Telegram over WhatsApp, (b) at-least-once delivery and why duplicates beat losses. *(The radius ADR was explicitly skipped at the user's request; the reasoning lives in the `WatchMatcher.Matches` comment and in this file.)*

## Roadmap after Phase 11 (one per session)
12 CI · 13 React UI (watch CRUD UI now has a real API to call already) · 14 Postgres/Aspire · 15 LLM enrichment.

## Handover note for a fresh session
This session ran Phases 6–8 plus the dynamic-watches design change, all committed. The app has been run manually several times against the real secondary-account Chrome (port 9222) — **no background process should currently be running** (confirmed killed at the end of this session), but a new session should double-check with `ps aux | grep MarketplaceWatcher` before assuming a clean start, since a prior mid-session cleanup attempt in this same session used a `pkill` pattern (`MarketplaceWatcher.dll`) that silently failed to match the actual apphost process name (`MarketplaceWatcher`, no `.dll`) and left it running unnoticed for a while. `watcher.db` (gitignored, local) currently has a real "sofa" watch (`Id=1`, active) plus whatever `Seen` rows accumulated from live scraping during this session's verification — safe to leave, delete, or reset (`rm src/MarketplaceWatcher/watcher.db*` then `dotnet run` re-migrates from scratch) as preferred.
