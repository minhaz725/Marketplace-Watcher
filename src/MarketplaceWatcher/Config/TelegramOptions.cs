using System.ComponentModel.DataAnnotations;

namespace MarketplaceWatcher.Config;

public sealed class TelegramOptions
{
    public const string Section = "Telegram";

    [Required(AllowEmptyStrings = false)]
    public string BotToken { get; set; } = "";

    [Required(AllowEmptyStrings = false)]
    public string ChatId { get; set; } = "";
}
