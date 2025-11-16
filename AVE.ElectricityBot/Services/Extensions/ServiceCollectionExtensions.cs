using AVE.ElectricityBot.Services.CommandHandlers;
using AVE.ElectricityBot.Services.Implementations;
using AVE.ElectricityBot.Services.Interfaces;
using AVE.ElectricityBot.Services.BackgroundServices;

namespace AVE.ElectricityBot.Services.Extensions;

/// <summary>
/// Розширення для реєстрації сервісів у DI контейнері
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Додати сервіси електробота
    /// </summary>
    public static IServiceCollection AddElectricityBotServices(this IServiceCollection services)
    {
        // Сервіси
        services.AddSingleton<IKeyboardService, KeyboardService>();
        services.AddSingleton<IMessageFormatterService, MessageFormatterService>();
        services.AddSingleton<IScheduleCacheService, ScheduleCacheService>();
        services.AddSingleton<ICallbackQueryHandler, CallbackQueryHandler>();
        services.AddSingleton<INotificationService, NotificationService>();
        services.AddSingleton<IChannelScraperService, ChannelScraperService>();
        
        // Обробники команд
        services.AddTransient<ICommandHandler, HelpCommandHandler>();
        services.AddTransient<ICommandHandler, StatusCommandHandler>();
        services.AddTransient<ICommandHandler, ScheduleCommandHandler>();
        services.AddTransient<ICommandHandler, TodayCommandHandler>();
        services.AddTransient<ICommandHandler, TomorrowCommandHandler>();
        services.AddTransient<ICommandHandler, GroupCommandHandler>();
        services.AddTransient<ICommandHandler, MenuCommandHandler>();
        services.AddTransient<ICommandHandler, StartCommandHandler>();
        services.AddTransient<ICommandHandler, OtherCommandHandler>();
        services.AddTransient<ICommandHandler, StatsCommandHandler>();
        services.AddTransient<ICommandHandler, SetGroupCommandHandler>();
        services.AddTransient<ICommandHandler, SettingsCommandHandler>();
        services.AddTransient<ICommandHandler, ScrapeCommandHandler>();
        
        // Обробник callback-запитів
        services.AddTransient<ICallbackQueryHandler, CallbackQueryHandler>();
        
        // Головний сервіс
        services.AddHostedService<TelegramMessageService>();
        services.AddHostedService<ChannelScraperBackgroundService>();
        
        return services;
    }
}
