using AVE.ElectricityBot.Configuration;
using AVE.ElectricityBot.Data.Abstractions;
using AVE.ElectricityBot.Services.Interfaces;
using AVE.ElectricityBot.YasnoApi;
using AVE.ElectricityBot.YasnoApi.Models;
using Microsoft.Extensions.Options;
using System.Text;
using Telegram.Bot.Types;

namespace AVE.ElectricityBot.Services.CommandHandlers;

/// <summary>
/// Обробник команди /other - перегляд інших груп
/// </summary>
public class OtherCommandHandler : BaseCommandHandler
{
    public OtherCommandHandler(
        IYasnoApiClient yasnoApiClient,
        IOptions<YasnoAddressConfiguration> addressConfig,
        IMessageFormatterService messageFormatter,
        ILogger<OtherCommandHandler> logger,
        IBotDataService dataService,
        IScheduleCacheService cacheService)
        : base(yasnoApiClient, addressConfig, messageFormatter, logger, dataService, cacheService)
    {
    }

    public override string CommandName => "other";
    public override string Description => "Перегляд інших груп відключень";

    public override async Task<string> HandleAsync(Message message, CancellationToken cancellationToken)
    {
        var parts = message.Text!.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length < 2)
        {
            return "🔍 <b>Перегляд іншої групи</b>\n\n" +
                   "Використання: <code>/other [група]</code>\n\n" +
                   "Приклади:\n" +
                   "• <code>/other 5.1</code> - перегляд групи 5.1\n" +
                   "• <code>/other 3.2</code> - перегляд групи 3.2\n\n" +
                   "Або використайте кнопку <b>\"🔍 Інша група\"</b> в меню.";
        }

        var groupInput = parts[1].Trim();
        var groupParts = groupInput.Split('.');

        if (groupParts.Length != 2 || 
            !int.TryParse(groupParts[0], out var group) || 
            !int.TryParse(groupParts[1], out var subgroup))
        {
            return "❌ Невірний формат групи!\n\n" +
                   "Формат: <b>X.Y</b> (наприклад: 5.1, 3.2)";
        }

        // Використовуємо кеш
        var outages = await CacheService.GetPlannedOutagesAsync(cancellationToken);

        if (outages == null)
        {
            return "❌ Не вдалося отримати розклад відключень";
        }

        var schedule = outages.GetSchedule(group, subgroup);

        if (schedule == null)
        {
            return $"❌ Розклад для групи {group}.{subgroup} не знайдено";
        }

        var sb = new StringBuilder();
        sb.AppendLine($"🔍 <b>ГРУПА {group}.{subgroup}</b>");
        sb.AppendLine();
        sb.AppendLine("┌─ 📅 <b>СЬОГОДНІ</b> ─────────────");
        MessageFormatter.AppendDailyScheduleSimple(sb, schedule.Today);
        sb.AppendLine("└────────────────────────");
        sb.AppendLine();
        sb.AppendLine("┌─ 📅 <b>ЗАВТРА</b> ───────────────");
        MessageFormatter.AppendDailyScheduleSimple(sb, schedule.Tomorrow);
        sb.AppendLine("└────────────────────────");

        return sb.ToString();
    }
}
