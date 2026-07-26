using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using MarketplaceWatcher.Config;
using MarketplaceWatcher.Domain;
using Microsoft.Extensions.Options;

namespace MarketplaceWatcher.Notifications;

public sealed class TelegramNotifier(
    HttpClient http,
    IOptions<TelegramOptions> options,
    ILogger<TelegramNotifier> logger) : INotifier
{
    private readonly TelegramOptions _telegram = options.Value;

    public async Task NotifyAsync(Listing listing, string watchName, CancellationToken cancellationToken)
    {
        var payload = new SendMessageRequest(
            ChatId: _telegram.ChatId,
            Text: BuildMessage(listing, watchName),
            ParseMode: "HTML");

        try
        {
            // Relative URL — the "bot{token}/" part lives in BaseAddress (see Program.cs).
            var response = await http.PostAsJsonAsync("sendMessage", payload, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                // Telegram puts the real reason ("chat not found", "bot was blocked") in the body,
                // not the status code — so log the body or you are debugging blind.
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                logger.LogError(
                    "Telegram send failed ({Status}) for {Title}: {Body}",
                    (int)response.StatusCode, listing.Title, body);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw; // Genuine shutdown — let the poll loop unwind.
        }
        catch (Exception ex)
        {
            // Any other failure (network down, Telegram outage, retries exhausted) must not
            // kill the polling loop. Losing one alert beats losing the whole watcher.
            logger.LogError(ex, "Telegram send threw for {Title}", listing.Title);
        }
    }

    private static string BuildMessage(Listing listing, string watchName)
    {
        var price = listing.Price == 0 ? "FREE" : $"NZ${listing.Price:0.##}";

        // HTML parse mode: Telegram rejects the whole message if a listing title contains
        // a bare "<" or "&", so every interpolated value gets encoded.
        return $"""
                <b>{Encode(listing.Title)}</b>
                {Encode(price)} · {Encode(listing.Location)}
                <i>watch: {Encode(watchName)}</i>

                {Encode(listing.Url)}
                """;
    }

    private static string Encode(string value) => WebUtility.HtmlEncode(value);

    private sealed record SendMessageRequest(
        [property: JsonPropertyName("chat_id")] string ChatId,
        [property: JsonPropertyName("text")] string Text,
        [property: JsonPropertyName("parse_mode")] string ParseMode);
}
