using AVE.ElectricityBot.Configuration;
using AVE.ElectricityBot.Data.Interfaces;
using AVE.ElectricityBot.Services.Interfaces;
using AVE.ElectricityBot.YasnoApi;
using Microsoft.Extensions.Options;
using Telegram.Bot.Types;

namespace AVE.ElectricityBot.Services.CommandHandlers;

/// <summary>
/// Обробник команди /scrape - ручний запуск скрапінгу каналів (для адміна)
/// </summary>
public class ScrapeCommandHandler : BaseCommandHandler
{
    private readonly IChannelScraperService _scraperService;

    public ScrapeCommandHandler(
        IYasnoApiClient yasnoApiClient,
        IOptions<YasnoAddressConfiguration> addressConfig,
        IMessageFormatterService messageFormatter,
        ILogger<ScrapeCommandHandler> logger,
        IBotDataService dataService,
        IScheduleCacheService cacheService,
        IChannelScraperService scraperService)
        : base(yasnoApiClient, addressConfig, messageFormatter, logger, dataService, cacheService)
    {
        _scraperService = scraperService;
    }

    public override string CommandName => "scrape";

    public override string Description => "Ручний запуск скрапінгу каналів (admin)";

    public override async Task<string> HandleAsync(Message message, CancellationToken cancellationToken)
    {
        try
        {
            Logger.LogInformation("Користувач {UserId} запустив ручний скрапінг каналів", message.From?.Id);

            await _scraperService.ScrapeAndForwardMessagesAsync(cancellationToken);

            return "✅ <b>Скрапінг каналів виконано</b>\n\n" +
                   "Повідомлення з Telegram каналів були оброблені та переслані користувачам за налаштуваннями.";
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Помилка під час ручного скрапінгу каналів");
            return "❌ <b>Помилка при скрапінгу каналів</b>\n\n" +
                   "Спробуйте ще раз або зверніться до адміністратора.";
        }
    }
}
