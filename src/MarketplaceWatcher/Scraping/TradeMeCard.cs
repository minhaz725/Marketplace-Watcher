namespace MarketplaceWatcher.Scraping;

/// <summary>One Trade Me Motors search result, as read off the card.</summary>
public sealed record TradeMeCard(
    string ListingId,
    string Title,
    decimal Price,
    string Location,
    int? OdometerKm,
    string Url);
