namespace MarketplaceWatcher.Api;

public sealed record CreateWatchRequest(
    string? Name,
    bool FreeOnly,
    decimal? MinPrice,
    decimal? MaxPrice,
    int? RadiusKm,
    string[]? MustIncludeKeywords,
    string[]? ExcludeKeywords,
    string? Condition,
    string? Category);
