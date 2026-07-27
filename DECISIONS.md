# Architecture Decision Records

Short records of decisions that were not obvious, with the reasoning and what we gave up.
Newest last. See [PROGRESS.md](PROGRESS.md) for the full build log.

---

## ADR-001 — Telegram, not WhatsApp

**Status:** accepted · **Date:** 2026-07-26

### Context

The alerting channel had to be something already on my phone. WhatsApp was the first preference;
Telegram was the fallback inherited from the House Hunt project.

### Decision

Use a Telegram bot.

### Why not WhatsApp

The only sanctioned route is the **WhatsApp Business Cloud API**, which needs a Meta business
account, a *dedicated* phone number not already registered to WhatsApp, and business verification.

The blocker is structural rather than administrative. WhatsApp prices on **who spoke first**:

- A user messaging you first opens a 24-hour window where free-form replies are free.
- **Business-initiated** messages are charged per message from the first one, and must use
  pre-approved templates rather than free-form text.

A marketplace alert is business-initiated by definition — nobody asked for it, the scraper found
something. So every alert costs money and every message-format change needs Meta's approval.

The one free path — keeping a 24-hour window open by messaging your own bot daily — is a manual
chore that silently breaks alerting when forgotten, and **Meta begins charging for in-window
non-template replies from 1 October 2026** anyway.

Unofficial libraries (Baileys, whatsapp-web.js) violate WhatsApp's ToS and risk a number ban.

Telegram: free, unlimited, no business account, no templates, ~60 seconds to set up.

### Consequences

- Alerts cost nothing and the message format is free to change.
- Recipients must be on Telegram. A group chat covers sharing with others; a bot cannot message
  someone who hasn't messaged it first.
- `INotifier` keeps the transport swappable, so WhatsApp would be a second implementation rather
  than a rewrite.

---

## ADR-002 — At-least-once delivery, not at-most-once

**Status:** accepted · **Date:** 2026-07-27 (supersedes the original design)

### Context

`GetNewAsync` originally answered "which listings are new?" *and* recorded them in `Seen` in one
committed call. The alert was attempted afterwards.

That ordering meant a Telegram failure — outage, rate limit, exhausted retries — left the listing
permanently marked as seen. **The alert was lost and nothing indicated it.** For a tool whose only
job is telling you about listings, a silent loss is the worst possible failure.

### Decision

Split the operation:

- `GetNewAsync` is **read-only** — it answers the question and commits nothing.
- `MarkSeenAsync(listings)` records them, and is called **only with listings whose alert sent**.
- `INotifier.NotifyAsync` returns `bool` so the worker can tell.

Failed sends stay unseen and are retried on the next poll.

### Consequences

- **Duplicates are now possible.** If a send succeeds but the process dies before `MarkSeenAsync`,
  you get that alert again next poll. This is the accepted cost.
- The tradeoff is deliberate: for an alerting tool, a duplicate is an annoyance and a loss is a
  failure. Exactly-once would need the send and the commit in one transaction, which is impossible
  across an HTTP call to a third party.
- A persistent Telegram outage means the retry set grows until it recovers. Bounded in practice by
  `daysSinceListed`, since old listings stop being scraped at all.

### Alternatives rejected

- **Keep at-most-once and accept losses** — considered and initially deferred, then fixed. Silent
  loss is not acceptable in the one thing the app exists to do.
- **A delivery-attempts column with a dead-letter queue** — more correct for a multi-user system,
  unjustified complexity for a personal tool with one recipient.

---

## ADR-003 — Verify scraper assumptions against a noise floor

**Status:** accepted · **Date:** 2026-07-27

### Context

Facebook Marketplace is undocumented and nondeterministic. Three times, a plausible conclusion
about its behaviour turned out to be wrong:

1. **`radius` URL params "worked"** — a narrow-vs-wide comparison returned different result sets.
   It was a false positive: the *same URL fetched twice* also returned different results.
2. **"Location strings are useless for filtering"** — 96% of a `sofa` search said
   `"Auckland, New Zealand"`. But that query has dense local supply; sparse queries are padded with
   nationwide results, and location varies a lot. One query shape is not a sample.
3. **A `daysSinceListed` result looked significant** — but the harness had no self-consistency
   check for that variant, so "genuinely different" and "unstable" were indistinguishable.

### Decision

Before believing any claim about Marketplace behaviour:

1. **Measure the system against itself first.** Fetch the same URL twice to establish the noise
   floor. A difference only counts if it clearly exceeds that.
2. **Vary the query shape.** Test at least one dense and one sparse search.
3. **Corroborate with an independent metric where possible.** `daysSinceListed` was confirmed by
   the share of cards tagged `"Just listed"` rising 16% → 77% — measured from the DOM, entirely
   separately from result-set overlap. Two unrelated signals agreeing is far stronger than one.

Reconnaissance is throwaway: a temporary file plus a CLI flag, deleted once it has answered.

### Consequences

- Confirmed: `daysSinceListed` works. Confirmed ignored: `radius`, `sortBy`.
- Recon costs a session but prevents building features on coincidences — the `radius` filter would
  have shipped as a no-op that appeared to work.

---

## ADR-004 — Search radius is environmental

**Status:** accepted · **Date:** 2026-07-27

### Context

Watches carry a `RadiusKm` field, and it is not applied.

### Decision

Leave it unapplied and document why. Radius is controlled by the Facebook account's saved
Marketplace location, not by this app.

### Why

- `latitude`/`longitude`/`radius` URL params are **ignored** (see ADR-003 for the method).
- Facebook's own picker offers **cities only**, so the circle cannot be centred on a home address.
- Client-side distance filtering would need per-listing coordinates. Cards give a display string
  (mostly just `"Auckland, New Zealand"`), so there is nothing precise to measure from.

### Consequences

- `RadiusKm` is stored but inert. A comment on `WatchMatcher.Matches` says so, to stop a future
  reader "fixing" the apparent omission.
- Coarse locality is instead enforced by `AllowedLocations` — a place-name filter, not a distance.
- Adjusting the real radius means changing it in the Facebook app.
