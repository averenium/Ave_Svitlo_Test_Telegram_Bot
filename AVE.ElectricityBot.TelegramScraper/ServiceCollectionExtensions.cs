using Microsoft.Extensions.DependencyInjection;

namespace AVE.ElectricityBot.TelegramScraper;

/// <summary>
/// Extension methods для реєстрації TelegramScraper в DI
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Додає TelegramChannelScraper до DI контейнеру
    /// </summary>
    public static IServiceCollection AddTelegramChannelScraper(this IServiceCollection services)
    {
        services.AddHttpClient<ITelegramChannelScraper, TelegramChannelScraper>();
        return services;
    }

    /// <summary>
    /// Додає TelegramChannelScraper з налаштуваннями HttpClient
    /// </summary>
    public static IServiceCollection AddTelegramChannelScraper(
        this IServiceCollection services,
        Action<HttpClient> configureClient)
    {
        services.AddHttpClient<ITelegramChannelScraper, TelegramChannelScraper>(configureClient);
        return services;
    }
}
