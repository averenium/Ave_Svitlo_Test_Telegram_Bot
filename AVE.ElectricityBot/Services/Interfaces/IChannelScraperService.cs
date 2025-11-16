namespace AVE.ElectricityBot.Services.Interfaces;

/// <summary>
/// Сервіс для скрапінгу каналів з Telegram каналів та пересилки повідомлень користувачам
/// </summary>
public interface IChannelScraperService
{
    /// <summary>
    /// Скрапити канали та переслати релевантні повідомлення користувачам
    /// </summary>
    Task ScrapeAndForwardMessagesAsync(CancellationToken cancellationToken = default);
}
