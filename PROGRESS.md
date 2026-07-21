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

### Commits (author = minhaz <minhaz725@gmail.com>, no co-author)
```
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
   ├─ Program.cs                       # Generic Host; explicitly registers PollingWorker
   ├─ appsettings.json                 # base config (has Watcher:Banner)
   ├─ appsettings.Development.json      # dev override (has Watcher:Banner)
   ├─ Properties/launchSettings.json    # dev-only launch profile (sets DOTNET_ENVIRONMENT=Development)
   └─ Worker/PollingWorker.cs          # BackgroundService heartbeat
```

## How to run / verify quickly
```bash
dotnet build src/MarketplaceWatcher
dotnet run --project src/MarketplaceWatcher        # Development (uses launchSettings) → DEV banner + heartbeat
```
Note: a plain `.NET` console app ignores SIGPIPE, so piping to `head` won't stop it — stop with Ctrl-C (or run in background and kill).

---

## ▶ NEXT: Phase 2 — Generic Host: DI + Options + secrets  `[core]`
Goal: replace today's **raw** `config["Watcher:Banner"]` with the **strongly-typed Options pattern**, and read a real secret from user-secrets.

Concrete steps (see CLAUDE.md §6 for the exact `WatcherOptions`/`Watch`/`GeoPoint` class shapes to use):
1. Add `Config/WatcherOptions.cs` with `WatcherOptions` (`Section = "Watcher"`, `PollIntervalSeconds`, `DefaultRadiusKm`, `Location`, `Watches`), plus `Watch` and `GeoPoint` (copy the code from CLAUDE.md §6 verbatim).
2. Bind it in `Program.cs`: `builder.Services.Configure<WatcherOptions>(builder.Configuration.GetSection(WatcherOptions.Section));`
   - Teach: `IOptions<T>` ≈ Spring `@ConfigurationProperties`; binding vs raw `config[...]`.
3. Inject `IOptions<WatcherOptions>` into `PollingWorker`; log the watch list (replace/retire the `Banner` demo hack).
4. Put the real `Watches` array from CLAUDE.md §6 into `appsettings.json` (the free-stuff + sofa + iPhone examples).
5. Secrets: `dotnet user-secrets set "Telegram:BotToken" "dummy-for-now"` (the `<UserSecretsId>` already exists in the csproj); read it via config and log that it loaded (don't log the value in real life).
6. ▶ Verify (CLAUDE.md §9 Phase 2): log the watch list + a secret pulled from config.

Teaching reminders for Phase 2: it's `[core]` → smaller steps, extra care. Explain the Options pattern mechanics (why `IOptions<T>`, singleton vs `IOptionsSnapshot`), and contrast EF-style config binding with the raw access we used in Phase 1.

## Roadmap after Phase 2 (one per session)
3 Web API `/health` · 4 EF Core + SQLite dedup (test-first) · 5 polling loop w/ scope · 6 Playwright CDP attach · 7 parse + 3 defenses (crux, test-first) · 8 wire pipeline · 9 Telegram · 10 all watches + radius · 11 polish/docs · 12 CI · 13 React UI · 14 Postgres/Aspire · 15 LLM enrichment.
