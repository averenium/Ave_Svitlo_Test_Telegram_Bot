using AVE.ElectricityBot.TelegramScraper.Models;

namespace AVE.ElectricityBot.TelegramScraper;

/// <summary>
/// Інтерфейс для scraping Telegram каналів
/// </summary>
public interface ITelegramChannelScraper
{
    /// <summary>
    /// Отримати інформацію про канал
    /// </summary>
    Task<TelegramChannelInfo?> GetChannelInfoAsync(string channelUsername, CancellationToken cancellationToken = default);

    /// <summary>
    /// Отримати останні N повідомлень з каналу
    /// </summary>
    Task<List<TelegramChannelMessage>> GetLatestMessagesAsync(
        string channelUsername, 
        int count = 10, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Отримати конкретне повідомлення за ID
    /// </summary>
    Task<TelegramChannelMessage?> GetMessageAsync(
        string channelUsername, 
        long messageId, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Отримати повідомлення за період часу
    /// </summary>
    Task<List<TelegramChannelMessage>> GetMessagesByDateRangeAsync(
        string channelUsername,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default);
}
