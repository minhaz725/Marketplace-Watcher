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

        app.MapGet("/api/watches", async (WatcherDbContext db) =>
            Results.Ok(await db.Watches.Where(w => w.IsActive).ToListAsync()));

        app.MapPost("/api/watches", async (CreateWatchRequest request, WatcherDbContext db, PollSignal signal) =>
        {
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
