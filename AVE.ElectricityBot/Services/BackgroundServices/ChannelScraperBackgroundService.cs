using AVE.ElectricityBot.Services.Interfaces;

namespace AVE.ElectricityBot.Services.BackgroundServices;

/// <summary>
/// Фонова служба, що запускає скрапінг каналів з Telegram каналів кожні 5 хвилин
/// </summary>
public class ChannelScraperBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ChannelScraperBackgroundService> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromMinutes(5);

    public ChannelScraperBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<ChannelScraperBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Фонова служба скрапінгу каналів запущена. Інтервал: {Interval} хвилин", 
            _interval.TotalMinutes);

        // Чекаємо 1 хвилину перед першим запуском
        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogDebug("Початок скрапінгу каналів з Telegram каналів...");

                using var scope = _serviceProvider.CreateScope();
                var scraperService = scope.ServiceProvider.GetRequiredService<IChannelScraperService>();

                await scraperService.ScrapeAndForwardMessagesAsync(stoppingToken);

                _logger.LogDebug("Скрапінг каналів завершено. Наступний запуск: {NextRun}",
                    DateTime.UtcNow.Add(_interval).ToString("HH:mm:ss"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка під час скрапінгу каналів з Telegram каналів");
            }

            // Чекаємо до наступного інтервалу
            await Task.Delay(_interval, stoppingToken);
        }

        _logger.LogInformation("Фонова служба скрапінгу каналів зупинена");
    }
}
