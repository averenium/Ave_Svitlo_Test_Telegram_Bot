using AVE.ElectricityBot.Configuration;
using AVE.ElectricityBot.Data.Abstractions;
using AVE.ElectricityBot.Services.Interfaces;
using AVE.ElectricityBot.YasnoApi;
using Microsoft.Extensions.Options;
using System.Text;
using Telegram.Bot.Types;

namespace AVE.ElectricityBot.Services.CommandHandlers;

/// <summary>
/// Обробник команди /status
/// </summary>
public class StatusCommandHandler : BaseCommandHandler
{
    public StatusCommandHandler(
        IYasnoApiClient yasnoApiClient,
        IOptions<YasnoAddressConfiguration> addressConfig,
        IMessageFormatterService messageFormatter,
        ILogger<StatusCommandHandler> logger,
        IBotDataService dataService,
        IScheduleCacheService cacheService)
        : base(yasnoApiClient, addressConfig, messageFormatter, logger, dataService, cacheService)
    {
    }

    public override string CommandName => "status";
    public override string Description => "Поточний стан електропостачання";

    public override async Task<string> HandleAsync(Message message, CancellationToken cancellationToken)
    {
        // Отримуємо групу та DSO користувача з БД
        var userGroupAndDso = await GetUserGroupAndDsoAsync(message.From!.Id, cancellationToken);
        if (!userGroupAndDso.HasValue)
        {
            return "📍 <b>Група не встановлена</b>\n\n" +
                   "Спочатку оберіть вашу групу:\n" +
                   "• Натисніть <b>\"📍 Моя група\"</b>\n" +
                   "• Або використайте /setgroup";
        }

        var (group, subgroup, dsoId) = userGroupAndDso.Value;
        var dsoName = dsoId == 301 ? "ДТЕК" : "ЦЕК";

        // Отримуємо розклад з API
        var schedule = await GetUserScheduleAsync(message.From.Id, cancellationToken);
        if (schedule == null)
        {
            return "❌ Розклад недоступний";
        }

        var now = DateTime.Now;
        var isOutage = MessageFormatter.IsCurrentlyInOutage(schedule.Today);

        var sb = new StringBuilder();
        sb.AppendLine("⚡ <b>ПОТОЧНИЙ СТАН</b>");
        sb.AppendLine($"🕐 {now:HH:mm:ss • dd.MM.yyyy}");
        sb.AppendLine();
        sb.AppendLine($"📍 Група: <code>{group}.{subgroup}</code> ({dsoName})");
        sb.AppendLine();

        if (isOutage)
        {
            var minutesFromMidnight = (int)(now - now.Date).TotalMinutes;
            var currentSlot = schedule.Today.Slots
                .Where(s => s.Type == YasnoApi.Models.OutageSlotType.Definite)
                .FirstOrDefault(s => s.Start <= minutesFromMidnight && s.End > minutesFromMidnight);

            if (currentSlot != null)
            {
                var endTime = now.Date.Add(currentSlot.GetEndTime());
                var remaining = endTime - now;

                sb.AppendLine("🔴 <b>ЗАРАЗ ВІДКЛЮЧЕННЯ</b>");
                sb.AppendLine($"⏳ Залишилось: <b>{(remaining.Hours > 0 ? $"{remaining.Hours} год " : "")}{remaining.Minutes} хв</b>");
                sb.AppendLine($"🕐 До: <code>{endTime:HH:mm}</code>");
            }
        }
        else
        {
            sb.AppendLine("✅ <b>ЗАРАЗ СВІТЛО Є</b>");

            var minutesFromMidnight = (int)(now - now.Date).TotalMinutes;
            var nextSlot = schedule.Today.Slots
                .Where(s => s.Type == YasnoApi.Models.OutageSlotType.Definite)
                .FirstOrDefault(s => s.Start > minutesFromMidnight);

            if (nextSlot != null)
            {
                var startTime = now.Date.Add(nextSlot.GetStartTime());
                var timeUntil = startTime - now;

                sb.AppendLine($"⏰ Наступне відключення: <code>{startTime:HH:mm}</code>");
                sb.AppendLine($"⏳ Через: <b>{(timeUntil.Hours > 0 ? $"{timeUntil.Hours} год " : "")}{timeUntil.Minutes} хв</b>");
            }
            else
            {
                sb.AppendLine("🎉 Більше відключень сьогодні немає!");
            }
        }

        return sb.ToString();
    }
}
