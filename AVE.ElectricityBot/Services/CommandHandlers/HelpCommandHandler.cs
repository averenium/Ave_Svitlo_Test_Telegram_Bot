using AVE.ElectricityBot.Configuration;
using AVE.ElectricityBot.Data.Abstractions;
using AVE.ElectricityBot.Services.Interfaces;
using AVE.ElectricityBot.YasnoApi;
using Microsoft.Extensions.Options;
using System.Text;
using Telegram.Bot.Types;

namespace AVE.ElectricityBot.Services.CommandHandlers;

/// <summary>
/// Обробник команди /help
/// </summary>
public class HelpCommandHandler : BaseCommandHandler
{
    public HelpCommandHandler(
        IYasnoApiClient yasnoApiClient,
        IOptions<YasnoAddressConfiguration> addressConfig,
        IMessageFormatterService messageFormatter,
        ILogger<HelpCommandHandler> logger,
        IBotDataService dataService,
        IScheduleCacheService cacheService)
        : base(yasnoApiClient, addressConfig, messageFormatter, logger, dataService, cacheService)
    {
    }

    public override string CommandName => "help";
    public override string Description => "Допомога";

    public override Task<string> HandleAsync(Message message, CancellationToken cancellationToken)
    {
        var response = """
               ❓ <b>ДОПОМОГА</b>
               
               <b>Як користуватися:</b>
               • Використовуйте кнопки меню для навігації
               • Або вводьте команди вручну
               
               <b>Команди:</b>
               /menu - 📋 Відкрити головне меню
               /status - ⚡ Поточний статус світла
               /schedule - 📅 Повний розклад
               /today - 📅 Розклад на сьогодні
               /tomorrow - 📅 Розклад на завтра
               /group - 📍 Інформація про групу
               /setgroup - 🔄 Встановити свою групу
               /other - 🔍 Інша група відключень
               /help - ❓ Ця довідка
               
               🔔 <b>Автоматичні повідомлення:</b>
               • При зміні статусу (світло є/немає)
               • При оновленні графіку
               • При зміні розкладу
               """;

        return Task.FromResult(response);
    }
}
