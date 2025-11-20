using AVE.ElectricityBot.Configuration;
using AVE.ElectricityBot.Data.Abstractions;
using AVE.ElectricityBot.Services.Interfaces;
using AVE.ElectricityBot.YasnoApi;
using Microsoft.Extensions.Options;
using Telegram.Bot.Types;

namespace AVE.ElectricityBot.Services.CommandHandlers;

/// <summary>
/// Обробник команди /setgroup - встановлення своєї групи
/// </summary>
public class SetGroupCommandHandler : BaseCommandHandler
{
    private readonly IBotDataService _dataService;

    public SetGroupCommandHandler(
        IYasnoApiClient yasnoApiClient,
        IOptions<YasnoAddressConfiguration> addressConfig,
        IMessageFormatterService messageFormatter,
        ILogger<SetGroupCommandHandler> logger,
        IBotDataService dataService,
        IScheduleCacheService cacheService) 
        : base(yasnoApiClient, addressConfig, messageFormatter, logger, dataService, cacheService)
    {
        _dataService = dataService;
    }

    public override string CommandName => "setgroup";
    public override string Description => "?? Встановити свою групу відключень";

    public override async Task<string> HandleAsync(Message message, CancellationToken cancellationToken)
    {
        // Перевіряємо чи є аргументи (формат: /setgroup 5.1)
        var parts = message.Text!.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length < 2)
        {
            return "?? <b>Встановлення групи відключень</b>\n\n" +
                   "Використання: <code>/setgroup [група]</code>\n\n" +
                   "Приклади:\n" +
                   "• <code>/setgroup 5.1</code>\n" +
                   "• <code>/setgroup 3.2</code>\n\n" +
                   "Або натисніть кнопку <b>\"?? Моя група\"</b> в меню для вибору зі списку.";
        }

        var groupInput = parts[1].Trim();

        // Валідація формату групи
        if (!IsValidGroupFormat(groupInput))
        {
            return "? <b>Невірний формат групи!</b>\n\n" +
                   "Група має бути у форматі <b>X.Y</b>, де:\n" +
                   "• X - номер групи (1-6)\n" +
                   "• Y - підгрупа (1-2)\n\n" +
                   "Приклади: <code>1.1</code>, <code>5.2</code>, <code>3.1</code>";
        }

        try
        {
            // Зберігаємо групу користувача
            await _dataService.UpdateUserGroupAsync(
                message.From!.Id,
                groupInput,
                cancellationToken);

            return $"? <b>Групу встановлено!</b>\n\n" +
                   $"Ваша група відключень: <b>{groupInput}</b>\n\n" +
                   $"Тепер ви можете:\n" +
                   $"• /status - побачити розклад для вашої групи\n" +
                   $"• /today - відключення сьогодні\n" +
                   $"• /tomorrow - відключення завтра";
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Помилка при збереженні групи");
            return "? Помилка при збереженні групи. Спробуйте пізніше.";
        }
    }

    private static bool IsValidGroupFormat(string group)
    {
        // Формат: X.Y де X = 1-6, Y = 1-2
        var parts = group.Split('.');
        
        if (parts.Length != 2)
            return false;

        if (!int.TryParse(parts[0], out var groupNum) || groupNum < 1 || groupNum > 6)
            return false;

        if (!int.TryParse(parts[1], out var subgroup) || subgroup < 1 || subgroup > 2)
            return false;

        return true;
    }
}
