namespace AVE.ElectricityBot.TelegramScraper.Models;

/// <summary>
/// Інформація про Telegram канал
/// </summary>
public class TelegramChannelInfo
{
    /// <summary>
    /// Username каналу (без @)
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Назва каналу
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Опис каналу
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Кількість підписників
    /// </summary>
    public int? SubscribersCount { get; set; }

    /// <summary>
    /// Посилання на аватар
    /// </summary>
    public string? AvatarUrl { get; set; }

    /// <summary>
    /// Чи є канал верифікованим
    /// </summary>
    public bool IsVerified { get; set; }
}
