using Microsoft.Playwright;

namespace MarketplaceWatcher.Scraping;

public sealed class CdpConnection : IAsyncDisposable
{
    private readonly IPlaywright _playwright;
    private readonly IBrowser _browser;

    private CdpConnection(IPlaywright playwright, IBrowser browser)
    {
        _playwright = playwright;
        _browser = browser;
    }

    public static async Task<CdpConnection> ConnectAsync(string cdpUrl = "http://localhost:9222")
    {
        var playwright = await Playwright.CreateAsync();
        var browser = await playwright.Chromium.ConnectOverCDPAsync(cdpUrl);
        return new CdpConnection(playwright, browser);
    }

    /// <summary>
    /// True while this connection can still be used. Chrome restarting leaves the cached
    /// IBrowser object alive but with no contexts, so checking IsConnected alone is not enough.
    /// </summary>
    public bool IsUsable => _browser.IsConnected && _browser.Contexts.Count > 0;

    public IBrowserContext Context =>
        _browser.Contexts.Count > 0
            ? _browser.Contexts[0]
            : throw new InvalidOperationException(
                "The attached Chrome has no browser context. It was probably closed or restarted — "
                + "start it again with --remote-debugging-port=9222.");

    public async ValueTask DisposeAsync()
    {
        try
        {
            // Obtained over CDP, so this disconnects rather than quitting the user's Chrome.
            // Wrapped because disposing an already-dead connection throws, and this runs
            // precisely when we suspect the connection is dead.
            await _browser.CloseAsync();
        }
        catch
        {
            // Nothing useful to do — we are discarding this connection either way.
        }

        _playwright.Dispose();
    }
}
