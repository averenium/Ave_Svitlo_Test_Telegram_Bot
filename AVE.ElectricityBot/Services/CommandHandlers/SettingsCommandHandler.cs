using AVE.ElectricityBot.Configuration;
using AVE.ElectricityBot.Data.Interfaces;
using AVE.ElectricityBot.Services.Interfaces;
using AVE.ElectricityBot.YasnoApi;
using Microsoft.Extensions.Options;
using Telegram.Bot.Types;

namespace AVE.ElectricityBot.Services.CommandHandlers;

/// <summary>
/// Обробник команди /settings - налаштування користувача
/// </summary>
public class SettingsCommandHandler : BaseCommandHandler
{
    public SettingsCommandHandler(
        IYasnoApiClient yasnoApiClient,
        IOptions<YasnoAddressConfiguration> addressConfig,
        IMessageFormatterService messageFormatter,
        ILogger<SettingsCommandHandler> logger,
        IBotDataService dataService,
        IScheduleCacheService cacheService)
        : base(yasnoApiClient, addressConfig, messageFormatter, logger, dataService, cacheService)
    {
    }

    public override string CommandName => "/settings";

    public override string Description => "Налаштування бота";

    public override async Task<string> HandleAsync(Message message, CancellationToken cancellationToken)
    {
        var user = await DataService.GetOrCreateUserAsync(
            message.From!.Id,
            message.Chat.Id,
            message.From.Username,
            message.From.FirstName,
            message.From.LastName,
            cancellationToken);

        var statusEmoji = user.Settings?.EnableChannelScraping == true ? "?" : "?";
        var statusText = user.Settings?.EnableChannelScraping == true ? "Увімкнено" : "Вимкнено";

        var text = $"?? <b>НАЛАШТУВАННЯ</b>\n\n" +
                   $"<b>Поточні параметри:</b>\n" +
                   $"• <b>DSO:</b> {FormatDsoName(user.DsoId)}\n" +
                   $"• <b>Група:</b> {user.GroupName ?? "Не вказана"}\n\n" +
                   $"<b>Сповіщення з каналів:</b>\n" +
                   $"{statusEmoji} {statusText}\n\n" +
                   $"Коли увімкнено, бот пересилає повідомлення з Telegram каналів (наприклад: @dnipro_svet), " +
                   $"що містять інформацію про ваш провайдер та групу відключень.\n\n" +
                   $"Переконайтеся, що вказали ваш DSO та групу.";

        return text;
    }

    private string FormatDsoName(int? dsoId)
    {
        return dsoId switch
        {
            301 => "ДТЕК (Дніпроенерго)",
            303 => "ЦЕК (Центренерго)",
            _ => "Не вказано"
        };
    }
}
