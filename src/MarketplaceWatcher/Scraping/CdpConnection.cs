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

    public IBrowserContext Context => _browser.Contexts[0];

    public async ValueTask DisposeAsync()
    {
        await _browser.CloseAsync();
        _playwright.Dispose();
    }
}
