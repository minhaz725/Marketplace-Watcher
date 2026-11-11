namespace MarketplaceWatcher.Scraping;

/// <summary>
/// One shared, self-healing CDP connection to the attached Chrome, used by every scraper.
/// Connecting spins up a Playwright driver process, so it is reused across polls rather than
/// opened per scrape — and shared so Facebook and Trade Me do not each start their own driver.
/// </summary>
public sealed class CdpConnectionProvider(ILogger<CdpConnectionProvider> logger) : IAsyncDisposable
{
    private CdpConnection? _connection;

    /// <summary>
    /// Returns a live connection, reconnecting if the cached one went stale. Chrome restarting
    /// leaves the cached IBrowser alive with no contexts, so IsConnected alone is not enough.
    /// </summary>
    public async Task<CdpConnection> GetAsync()
    {
        if (_connection is { IsUsable: true })
        {
            return _connection;
        }

        if (_connection is not null)
        {
            logger.LogWarning("CDP connection went stale (Chrome closed or restarted); reconnecting");
            await _connection.DisposeAsync();
            _connection = null;
        }

        return _connection = await CdpConnection.ConnectAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }
    }
}
