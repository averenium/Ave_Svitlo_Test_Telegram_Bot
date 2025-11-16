using Microsoft.Extensions.DependencyInjection;

namespace AVE.ElectricityBot.YasnoApi.Extensions;

/// <summary>
/// Методи розширення для реєстрації Yasno API сервісів
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Додає Yasno API клієнт до колекції сервісів
    /// </summary>
    /// <param name="services">Колекція сервісів</param>
    /// <returns>Колекція сервісів для ланцюгових викликів</returns>
    public static IServiceCollection AddYasnoApiClient(this IServiceCollection services)
    {
        services.AddHttpClient<IYasnoApiClient, YasnoApiClient>(client =>
        {
            client.BaseAddress = new Uri("https://app.yasno.ua/api/blackout-service/public/");
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        return services;
    }
}
