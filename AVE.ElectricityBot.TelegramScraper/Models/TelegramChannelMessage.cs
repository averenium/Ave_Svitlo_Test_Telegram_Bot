namespace AVE.ElectricityBot.TelegramScraper.Models;

/// <summary>
/// Повідомлення з Telegram каналу
/// </summary>
public class TelegramChannelMessage
{
    /// <summary>
    /// ID повідомлення
    /// </summary>
    public long MessageId { get; set; }

    /// <summary>
    /// Дата публікації
    /// </summary>
    public DateTime PublishedAt { get; set; }

    /// <summary>
    /// Текст повідомлення
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Посилання на повідомлення
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Кількість переглядів
    /// </summary>
    public int? Views { get; set; }

    /// <summary>
    /// Чи є медіа (фото, відео)
    /// </summary>
    public bool HasMedia { get; set; }

    /// <summary>
    /// Посилання на медіа
    /// </summary>
    public List<string> MediaUrls { get; set; } = new();
}
