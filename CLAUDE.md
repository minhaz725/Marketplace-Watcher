# CLAUDE.md — Marketplace Watcher (a .NET + React learning project / portfolio piece)

> Source of truth for how Claude Code should build this **with** me, not just **for** me.
> Read §3 ("How to teach me") before writing any code.

---

## 1. Purpose & goals

A personal Facebook Marketplace alert tool: watch a broad "free stuff near me" search plus specific
keyword searches (e.g. *sofa*, *iPhone 16 Pro Max*), poll gently on my **secondary** account, dedupe,
and Telegram me only genuinely new listings.

Deliberately a **.NET port of my House Hunt 2026 project** (§2). Goals, in priority order:

1. **Learn modern C#/.NET (and a little React).** Not output — understanding.
2. **A working app** I actually run.
3. **A portfolio piece** targeting NZ **.NET (C#) + React** roles (see §11 for the JD mapping).

If 1 and 2 conflict, favour 1.

---

## 2. Who I am (the learner)

Backend engineer with a Java/Spring Boot + Go background and event-driven / distributed-systems experience.
Good mental model of *architecture* (workers, DI, dedup, migrations, HTTP clients, CDP scraping, Telegram
alerting) from building the equivalent in Spring Boot.

**Treat me as a genuine .NET learner.** My C#/.NET is old and shallow and I don't know today's idioms. Don't
assume that knowing the Spring version of a concept means I know the .NET version or the language mechanics.
**Frontend is my weakest area** — go slower and more explicit in the React phase. When in doubt, explain.

Prior art — **House Hunt 2026** (the project this ports):
> Event-driven Spring Boot service (PostgreSQL, Flyway, **Playwright-CDP**, Ollama) that scrapes Auckland
> listings from Facebook/TradeMe, scores them, and sends **Telegram** alerts.

Use House Hunt / Spring Boot as an *anchor*, then teach the .NET idiom and mechanics properly.

---

## 3. How to teach me — READ THIS EVERY SESSION

- **Default to explaining.** Err toward more explanation. Spring/House Hunt analogies locate the concept; they
  are **not** a substitute for the .NET specifics.
- **Tiny steps.** One new .NET idea per step.
- **Anchor, then teach:** (a) the House Hunt/Spring equivalent, (b) the .NET idiom, (c) the language mechanics I
  might not know, (d) the code.
- **`[anchor]` vs `[core]` in §9:** `[anchor]` = you'll recognise the shape from House Hunt (motivate fast, but
  still teach the .NET mechanics); `[core]` = new material, smaller steps, extra care. **React phase is `[core]`.**
- **Small diffs** (5–30 lines). No whole-file dumps except when scaffolding a new file.
- **Pause and hand back.** After each step: the command, what I should see, why. Then stop.
- **Name the idiom**; no magic in `Program.cs`.
- **If unsure whether I know something, explain it.**
- **Checkpoints:** end each phase with a 3-bullet recap + "you can now do X".

If I say "go faster" / "just show me", relax for that stretch.

---

## 4. Tech stack & why

| Concern | Choice | House Hunt / Spring analogue | Why |
|---|---|---|---|
| Platform | **.NET 10** (LTS, C# 14) | JVM + JDK | Current LTS |
| App host | **.NET Generic Host** (Worker + web) | Spring Boot app | Worker + API in one process |
| Web API | **ASP.NET Core Minimal APIs** | Spring MVC controllers | Serves the React UI |
| Frontend | **Vite + React + TypeScript** | (Nuxt in House Hunt) | Half the target-job stack; TS = quality signal |
| Background loop | **`BackgroundService`** | `@Scheduled` worker | The polling loop |
| Persistence | **EF Core + SQLite** (→ Postgres later) | Spring Data JPA + Postgres/Flyway | Dedup store; SQLite = zero infra to start |
| Migrations | **EF Core migrations** | **Flyway** | Note the contrast |
| Browser automation | **Playwright for .NET over CDP** | Playwright-Java `connectOverCDP` | Port of your proven approach |
| HTTP out | **`IHttpClientFactory` typed client** (+ Polly) | `WebClient` + Resilience4j | Idiomatic outbound HTTP |
| Notify | **Telegram bot** | House Hunt's Telegram alerts | Already your pattern |
| Config/secrets | **`appsettings.json` + Options + user-secrets** | `application.yml` + `@ConfigurationProperties` | Secrets out of source |
| Tests | **xUnit** (+ TDD for pure logic) | JUnit + TDD | JD calls out TDD explicitly (§11) |
| CI | **GitHub Actions** | Jenkins | Automated tests + trunk-based signal (§11) |
| Orchestration | **Aspire — deferred (Phase 14)** | Spring Cloud + compose + otel | Only once there are 2+ services |

**IDE (macOS):** JetBrains **Rider**. VS Code + **C# Dev Kit** also fine.

**Scraping = CDP attach (not launch).** Attach to a Chrome you start (secondary account, logged in). No
`playwright install` / browser download needed.

---

## 5. Ground rules (personal-use guardrails)

- **Secondary account only**, isolated in its own Chrome profile:
  ```bash
  "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome" \
    --remote-debugging-port=9222 \
    --user-data-dir="$HOME/.fb-secondary-chrome"
  ```
- **Attach, never launch/log in.** `ConnectOverCDPAsync("http://localhost:9222")`, reuse `browser.Contexts[0]`.
  **Never** `NewContextAsync()`. Read-only observer; no credentials touch the app.
- **Gentle & local:** jittered ~60–120s interval, one page at a time, own machine / residential IP.

---

## 6. Configuration model & filters

Each **watch** is a config object (✅ ship now, ⏳ later):

| Field | Type | | Meaning |
|---|---|---|---|
| `name` | `string?` | ✅ | Keyword. `null` = match everything (free watch) |
| `freeOnly` | `bool` | ✅ | Only 0 NZD. "Any free item in radius → alert" |
| `minPrice` / `maxPrice` | `decimal?` | ✅ | NZD range; ignored when `freeOnly`. `minPrice` > 0 dodges $1 scam decoys |
| `radiusKm` | `int?` | ✅ | Falls back to `DefaultRadiusKm` (**5**) |
| `excludeKeywords` | `string[]` | ✅ | Drop noise: "wanted", "iso", "parts", "cover only"… (biggest S/N lever) |
| `mustIncludeKeywords` | `string[]` | ⏳ | Title must contain all — precision (e.g. force "pro max") |
| `condition` | `string?` | ⏳ | new / used |
| `category` | `string?` | ⏳ | Marketplace category |

**`appsettings.json` example:**
```json
{
  "Watcher": {
    "PollIntervalSeconds": 90,
    "DefaultRadiusKm": 5,
    "Location": { "Lat": -36.8485, "Lng": 174.7633 },
    "Watches": [
      { "name": null, "freeOnly": true, "radiusKm": 5,
        "excludeKeywords": ["wanted", "iso", "looking for"] },
      { "name": "sofa", "minPrice": 0, "maxPrice": 200, "radiusKm": 5,
        "excludeKeywords": ["wanted", "parts", "cover only"] },
      { "name": "iPhone 16 Pro Max", "minPrice": 200, "maxPrice": 1500, "radiusKm": 5,
        "mustIncludeKeywords": ["pro max"],
        "excludeKeywords": ["case", "screen", "wanted", "parts", "cracked"] }
    ]
  }
}
```

**Bound options (Phase 2):**
```csharp
public sealed class WatcherOptions
{
    public const string Section = "Watcher";
    public int PollIntervalSeconds { get; set; } = 90;
    public int DefaultRadiusKm { get; set; } = 5;
    public GeoPoint Location { get; set; } = new();
    public List<Watch> Watches { get; set; } = [];
}
public sealed class Watch
{
    public string? Name { get; set; }
    public bool FreeOnly { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public int? RadiusKm { get; set; }
    public string[] MustIncludeKeywords { get; set; } = [];
    public string[] ExcludeKeywords { get; set; } = [];
    public string? Condition { get; set; }
    public string? Category { get; set; }
}
public sealed class GeoPoint { public double Lat { get; set; } = -36.8485; public double Lng { get; set; } = 174.7633; }
```

---

## 7. Target structure (grow into it — don't scaffold it all at once)

```
MarketplaceWatcher/
├─ CLAUDE.md · DECISIONS.md · README.md
├─ .github/workflows/ci.yml         # GitHub Actions (Phase 12)
├─ MarketplaceWatcher.sln
├─ src/MarketplaceWatcher/
│  ├─ Program.cs · MarketplaceWatcher.csproj
│  ├─ appsettings.json · appsettings.Development.json
│  ├─ Properties/launchSettings.json
│  ├─ Domain/Listing.cs             # record + content Signature
│  ├─ Config/WatcherOptions.cs
│  ├─ Data/WatcherDbContext.cs      # Seen table keyed on Signature
│  ├─ Api/Endpoints.cs              # minimal-API routes for the UI (Phase 13)
│  ├─ Scraping/{CdpConnection, IMarketplaceScraper, MarketplaceScraper, TextReconstructor}.cs
│  ├─ Notifications/{INotifier, TelegramNotifier}.cs
│  └─ Worker/PollingWorker.cs
├─ web/                             # Vite + React + TS (Phase 13)
│  └─ src/{App.tsx, api.ts, components/}
└─ tests/MarketplaceWatcher.Tests/  # xUnit — TDD for pure logic
```

---

## 8. Conventions

- `Nullable` + `ImplicitUsings` **enabled**; file-scoped namespaces; one class per file.
- Async methods end `Async`, take a `CancellationToken`.
- Constructor injection only; strongly-typed Options; no scattered raw config strings.
- Records for data (`Listing`); classes for services.
- **Dedup on `Signature`** (normalized content hash), never on URL/listing-id.
- **TDD for pure logic** — write the xUnit test first for `TextReconstructor`, filter matching, `Signature`,
  and dedup. These are deterministic and the JD prizes TDD (§11).
- **Trunk-based git** — short-lived branches, small PRs, `main` always green (CI-gated). Commit in logical steps.

---

## 9. Phased roadmap

Runnable milestone per phase. **One phase per session; stop at every "▶ Verify".** Follow §3.

### Phase 0 — Environment `[anchor]`
Install .NET 10 SDK + Rider + Node (for the UI later). ▶ **Verify:** `dotnet --version` → 10.x.

### Phase 1 — Scaffold + full structure & config tour `[core]`
Not just files that build — a clear picture of layout and config layering. Walk through each, small + explained:
- **1a.** `dotnet new sln`; `dotnet new worker -o src/MarketplaceWatcher`; `dotnet sln add`. (`.sln` ≈ multi-module aggregator.)
- **1b.** Tour generated files: `.csproj`, `Program.cs`, `appsettings.json`, `Properties/launchSettings.json`, `obj/`+`bin/`.
- **1c.** Edit `.csproj` — nullable + implicit usings + namespace (project file ≈ `pom.xml`, `<PackageReference>` ≈ a dependency).
- **1d.** `Program.cs` host bootstrap (≈ Spring Boot `main`); `builder.Services` = DI container.
- **1e.** A heartbeat `PollingWorker` (`BackgroundService`, `ExecuteAsync`, `CancellationToken`, `ILogger<T>`).
- **1f.** Config sources & precedence (later wins): `appsettings.json` → `appsettings.{Env}.json` → user-secrets →
  env vars → CLI args (≈ Spring property sources + profiles). `launchSettings.json` sets `DOTNET_ENVIRONMENT`;
  `dotnet user-secrets init` for the Telegram token.
▶ **Verify:** `dotnet run` logs the heartbeat; a Development override wins; I can explain each file.

### Phase 2 — Generic Host: DI + Options + secrets `[core]`
Bind `WatcherOptions` (§6) via the Options pattern; inject `IOptions<WatcherOptions>`; read the token from user-secrets.
▶ **Verify:** log the watch list + a secret from config.

### Phase 3 — Minimal web API (grows with the UI) `[anchor]`
Start with `/health`. This API will grow in Phase 13 to serve the React UI (listings/watches/status/poll) with CORS.
▶ **Verify:** `/health` returns 200.

### Phase 4 — EF Core + SQLite dedup store `[core]` (test-first)
`WatcherDbContext`; `Seen` table keyed on `Signature`; first **migration**; `GetNewAsync`. Contrast EF migrations vs Flyway.
▶ **Verify:** run twice, same candidates → 0 new; dedup covered by a test.

### Phase 5 — BackgroundService polling loop `[anchor]`
Flesh out `PollingWorker`; `Task.Delay(jitter, token)`; **create a scope** for the scoped `DbContext` (the new gotcha).
▶ **Verify:** "polled, 0 new" on a jittered interval; clean Ctrl-C.

### Phase 6 — Playwright .NET: attach over CDP `[core]`
Add `Microsoft.Playwright` (no browser download). `CdpConnection`: `ConnectOverCDPAsync`, `Contexts[0]`,
`NewPageAsync`; navigate to a search URL; dump `innerText`. Port of your Java `connectOverCDP` + `contexts().get(0)`.
▶ **Verify:** a page in *your* logged-in Chrome loads; we print text.

### Phase 7 — Parse cards + the three defenses `[core]` (test-first)  ⟵ crux
1. **Virtualized feed** → harvest each card *as it renders* while scrolling.
2. **CSS-shuffled decoy text** → `TextReconstructor`: sort visible chars by on-screen x-position; drop hidden nodes.
3. **Content signature** → hash of normalized (title+price+location).
Apply `excludeKeywords`/`mustIncludeKeywords`/price/free filters. Capture the **live** Marketplace DOM here.
▶ **Verify:** parsed listings + signatures match screen; `TextReconstructor` + filters have unit tests.

### Phase 8 — Wire the pipeline `[anchor]`
Worker → attach → scrape each watch → parse+filter → `GetNewAsync` → collect new. Jitter.
▶ **Verify:** first run stores a batch; later runs mostly 0 new.

### Phase 9 — Telegram notifier `[anchor]`
`INotifier` + typed `HttpClient` (+ Polly); token from user-secrets; message with title/price/location/url.
▶ **Verify:** a real new listing pings my phone.

### Phase 10 — All watches incl. "free near me" `[anchor]`
Full config list; radius (default 5 km) + client-side distance filter around Auckland.
▶ **Verify:** free-only and keyword watches both fire from config.

### Phase 11 — Polish + docs (on-call / decisions signal) `[anchor]`
Structured logging (optionally Serilog); error backoff; `/status`; graceful shutdown. **README** (run steps),
**DECISIONS.md** (ADR-style: why CDP-attach, signature dedup, SQLite→Postgres, tradeoffs), and a short
**on-call runbook** (symptoms → checks → fixes). These directly serve the JD's "clear technical options" +
"incident response".

### Phase 12 — CI pipeline (GitHub Actions) `[core]`
`.github/workflows/ci.yml`: on PR → `dotnet restore` → `build` → `test` → `dotnet format --verify-no-changes`.
Protect `main` (green required). Signals "automated testing setups" + trunk-based (§11).
▶ **Verify:** a PR runs the workflow; a failing test blocks merge.

### Phase 13 — React UI (the capstone — priority, not optional) `[core]`
The React half of the target stack. Keep it small but clean.
**Backend prep (minimal APIs, `Api/Endpoints.cs`):**
- `GET /api/listings?watch=&limit=` — past/seen listings, newest first
- `GET /api/watches` — configured watches (read-only)
- `GET /api/status` — last poll time, per-watch counts, running state
- `POST /api/poll` — trigger an immediate poll (signal the worker, e.g. via a `Channel`)
- CORS policy for the Vite dev server (`http://localhost:5173`)
**Frontend (`web/`, Vite + React + TS):** a tiny `api.ts` fetch client + 3 components — a **listings table**
(title, price, location, posted, link, matched watch), a **status bar** with a **“Poll now”** button, and a
read-only **watches panel**. No router/state library needed.
▶ **Verify:** `npm run dev` shows past listings from the API; “Poll now” triggers a scrape; status updates.
_(Optional stretch: edit watches from the UI → PUT config.)_

### Phase 14 — (Optional) Postgres + Aspire + container/cloud `[core, later]`
SQLite → **Postgres** (House Hunt parity); **Aspire** to orchestrate worker + Postgres + dashboard; **Dockerfile**;
a deploy note (AWS ECS/Fargate — Xero is AWS-heavy). Serves the JD's "cloud infrastructure".

### Phase 15 — (Optional stretch) local-LLM enrichment `[later]`
Mirror House Hunt's Ollama scoring: score listings against my criteria before alerting.

---

## 10. Decisions

- **Notifier:** Telegram (confirmed). **Radius:** default **5 km**, per-watch override.
- **Filters (now):** name, price range, free-only, radius, exclude-keywords. (later: must-include, condition, category.)
- **Frontend:** Vite + React + TypeScript, deliberately minimal.
- **DB:** SQLite to start; Postgres in Phase 14. **Scraping:** CDP-attach, secondary account (§5).

## 11. Job-targeting map (NZ .NET + React roles, e.g. Xero "Practice Tools")

| JD requirement | Covered by | Status |
|---|---|---|
| C#/.NET, scalable design | Phases 1–10 (host, EF Core, workers, resilience) | ✅ core |
| React | Phase 13 UI | ✅ added |
| TDD / quality-first | test-first logic (§8) + Phase 4/7 | ✅ added |
| Automated testing setups / CI | Phase 12 GitHub Actions | ✅ added |
| Trunk-based development | §8 git workflow + main-green CI | ✅ added |
| AI dev agents (Copilot/Claude Code/Windsurf) | this whole project is built via Claude Code + CLAUDE.md | ✅ inherent |
| Debugging across full stack | React → API → worker → scraper → DB, all yours | ✅ inherent |
| Incident response / on-call | `/health`, `/status`, backoff, graceful shutdown + runbook (Phase 11) | ✅ added |
| Cloud infrastructure | Dockerfile + deploy note (Phase 14) | ⚠️ gesture, not enterprise scale |
| Research & deliver clear technical options | DECISIONS.md ADRs (Phase 11) | ✅ added |
| Modernising legacy / micro-frontend events | — | ❌ can't authentically demo solo; **speak to the concept in interview** |
| Code review / team collaboration | PR + CI workflow (proxy only) | ⚠️ solo project; be ready to discuss |

**Don't fake the ❌/⚠️ items.** Better to name them honestly and talk about the ideas in interview than to
bolt on hollow versions.

## 12. Known unknown

- **Current Marketplace DOM selectors** — captured live in Phase 7; kept in one file for easy fixes.

## 13. Quick C# ↔ Java/House Hunt reference

- `record` ≈ POJO (no Lombok) · properties ≈ getters/setters · `string?` ≈ compile-time nullability
- `async Task<T>`/`await` ≈ native · LINQ ≈ Streams · DI Singleton/Scoped/Transient ≈ singleton/request/prototype beans
- `IOptions<T>` ≈ `@ConfigurationProperties` · `.sln` ≈ aggregator · `.csproj` ≈ `pom.xml` · `<PackageReference>` ≈ dependency
- config layering (json < env json < user-secrets < env vars < args) ≈ Spring property sources + profiles
- `BackgroundService` ≈ `@Scheduled` · `IHttpClientFactory` ≈ `WebClient` bean · **EF migrations ≈ Flyway** (code- vs SQL-first)
- `ConnectOverCDPAsync` ≈ `connectOverCDP` · `Contexts[0]` ≈ `contexts().get(0)` · no checked exceptions
- **React/TS**: components ≈ small view functions · `useState`/`useEffect` for state + fetch · `api.ts` = typed fetch client
