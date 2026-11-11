using MarketplaceWatcher.Data;
using MarketplaceWatcher.Worker;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace MarketplaceWatcher.Api;

public static class Endpoints
{
    public static void MapWatcherEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

        // Liveness (/health) says the process is up. This says whether it is doing its job.
        app.MapGet("/api/status", async (WatcherStatus status, WatcherDbContext db) =>
        {
            var activeWatches = await db.Watches.Where(w => w.IsActive).CountAsync();
            var seenCount = await db.Seen.CountAsync();

            return Results.Ok(new
            {
                healthy = status.IsHealthy,
                lastPollAt = status.LastPollAt,
                lastSuccessfulPollAt = status.LastSuccessfulPollAt,
                minutesSinceLastSuccess = status.LastSuccessfulPollAt is { } t
                    ? Math.Round((DateTimeOffset.UtcNow - t).TotalMinutes, 1)
                    : (double?)null,
                lastPollNewCount = status.LastPollNewCount,
                totalAlertsSent = status.TotalAlertsSent,
                consecutiveFailures = status.ConsecutiveFailures,
                lastError = status.LastError,
                activeWatches,
                seenListings = seenCount,
            });
        });

        // Phase 13's "Poll now" button, available early since PollSignal already exists.
        app.MapPost("/api/poll", (PollSignal signal) =>
        {
            signal.Trigger();
            return Results.Accepted();
        });

        app.MapGet("/api/listings", async (WatcherDbContext db, string? watch, string? source, int? limit) =>
        {
            // IQueryable composes before executing: the filter and limit become SQL WHERE and
            // LIMIT, so we never pull the whole table into memory to trim it.
            var query = db.Seen.Where(s => s.HiddenAt == null);

            if (!string.IsNullOrWhiteSpace(watch))
            {
                query = query.Where(s => s.WatchName == watch);
            }

            // "Facebook" or "TradeMe" — the dashboard's two tabs. Omitted = both.
            if (!string.IsNullOrWhiteSpace(source))
            {
                query = query.Where(s => s.Source == source);
            }

            var listings = await query
                .OrderByDescending(s => s.SeenAt)
                .Take(Math.Clamp(limit ?? 50, 1, 500))
                .Select(s => new
                {
                    s.Signature,
                    s.Title,
                    s.Price,
                    s.Location,
                    s.Url,
                    s.WatchName,
                    s.SeenAt,
                    s.SoldAt,
                    s.ContactedAt,
                    s.Source,
                })
                .ToListAsync();

            return Results.Ok(listings);
        });

        // Toggle rather than set: clicking again undoes a misclick, and it keeps the client
        // from having to know the current state to flip it.
        app.MapPost("/api/listings/{signature}/sold", async (string signature, WatcherDbContext db) =>
        {
            var listing = await db.Seen.FirstOrDefaultAsync(s => s.Signature == signature);
            if (listing is null)
            {
                return Results.NotFound();
            }

            listing.SoldAt = listing.SoldAt is null ? DateTime.UtcNow : null;
            await db.SaveChangesAsync();

            return Results.Ok(new { signature, soldAt = listing.SoldAt });
        });

        // Same toggle shape as /sold: tick on, click again to tick off.
        app.MapPost("/api/listings/{signature}/contacted", async (string signature, WatcherDbContext db) =>
        {
            var listing = await db.Seen.FirstOrDefaultAsync(s => s.Signature == signature);
            if (listing is null)
            {
                return Results.NotFound();
            }

            listing.ContactedAt = listing.ContactedAt is null ? DateTime.UtcNow : null;
            await db.SaveChangesAsync();

            return Results.Ok(new { signature, contactedAt = listing.ContactedAt });
        });

        // Soft delete: hide the row rather than removing it. Dedup is keyed on Signature in this
        // same table, so deleting it would make the next poll re-alert on a still-live listing.
        app.MapDelete("/api/listings/{signature}", async (string signature, WatcherDbContext db) =>
        {
            var hidden = await db.Seen
                .Where(s => s.Signature == signature && s.HiddenAt == null)
                .ExecuteUpdateAsync(set => set.SetProperty(s => s.HiddenAt, DateTime.UtcNow));

            return hidden == 0 ? Results.NotFound() : Results.NoContent();
        });

        app.MapGet("/api/watches", async (WatcherDbContext db) =>
            Results.Ok(await db.Watches.Where(w => w.IsActive).ToListAsync()));

        app.MapPost("/api/watches", async (CreateWatchRequest request, WatcherDbContext db, PollSignal signal) =>
        {
            // The UI validates the same rules, but that is for feedback, not correctness —
            // anything can POST here. Returning the reason as text so the client can show it.
            if (!request.FreeOnly && string.IsNullOrWhiteSpace(request.Name))
            {
                return Results.BadRequest("A watch needs a search term unless it is free-only.");
            }

            if (request.MinPrice is < 0 || request.MaxPrice is < 0)
            {
                return Results.BadRequest("Prices cannot be negative.");
            }

            if (request.MinPrice is { } min && request.MaxPrice is { } max && min > max)
            {
                return Results.BadRequest("Min price cannot be higher than max price.");
            }

            if (request.DaysSinceListed is { } days && (days < 1 || days > 90))
            {
                return Results.BadRequest("Days must be between 1 and 90.");
            }

            // A keyword in both lists makes the watch unsatisfiable — every listing is
            // simultaneously required and rejected, so it silently matches nothing forever.
            var contradictions = (request.MustIncludeKeywords ?? [])
                .Intersect(request.ExcludeKeywords ?? [], StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (contradictions.Count > 0)
            {
                return Results.BadRequest(
                    $"These keywords are in both must-include and exclude, so nothing could ever "
                    + $"match: {string.Join(", ", contradictions)}");
            }

            var watch = new WatchEntity
            {
                Name = request.Name,
                FreeOnly = request.FreeOnly,
                MinPrice = request.MinPrice,
                MaxPrice = request.MaxPrice,
                RadiusKm = request.RadiusKm,
                MustIncludeKeywords = request.MustIncludeKeywords ?? [],
                ExcludeKeywords = request.ExcludeKeywords ?? [],
                Condition = request.Condition,
                Category = request.Category,
                // Blank means "use the default recipient", so normalise "" to null.
                ChatId = string.IsNullOrWhiteSpace(request.ChatId) ? null : request.ChatId.Trim(),
                DaysSinceListed = request.DaysSinceListed,
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow,
            };

            db.Watches.Add(watch);
            await db.SaveChangesAsync();
            signal.Trigger();

            return Results.Created($"/api/watches/{watch.Id}", watch);
        });

        app.MapDelete("/api/watches/{id:int}", async (int id, WatcherDbContext db) =>
        {
            var watch = await db.Watches.FindAsync(id);
            if (watch is null)
            {
                return Results.NotFound();
            }

            watch.IsActive = false;
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }
}
