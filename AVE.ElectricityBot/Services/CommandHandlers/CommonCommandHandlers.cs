using AVE.ElectricityBot.Configuration;
using AVE.ElectricityBot.Data.Interfaces;
using AVE.ElectricityBot.Services.Interfaces;
using AVE.ElectricityBot.YasnoApi;
using Microsoft.Extensions.Options;
using System.Text;
using Telegram.Bot.Types;

namespace AVE.ElectricityBot.Services.CommandHandlers;

public class ScheduleCommandHandler : BaseCommandHandler
{
    public ScheduleCommandHandler(
        IYasnoApiClient yasnoApiClient,
        IOptions<YasnoAddressConfiguration> addressConfig,
        IMessageFormatterService messageFormatter,
        ILogger<ScheduleCommandHandler> logger,
        IBotDataService dataService,
        IScheduleCacheService cacheService)
        : base(yasnoApiClient, addressConfig, messageFormatter, logger, dataService, cacheService)
    {
    }

    public override string CommandName => "schedule";
    public override string Description => "Розклад на сьогодні та завтра";

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
        var sb = new StringBuilder();
        
        sb.AppendLine("⚡ <b>ЕЛЕКТРОПОСТАЧАННЯ</b>");
        sb.AppendLine($"🕐 {now:HH:mm:ss} • {now:dd.MM.yyyy}");
        sb.AppendLine($"📍 Група: <code>{group}.{subgroup}</code> ({dsoName})");
        sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━");
        sb.AppendLine();

        // === ПОТОЧНИЙ СТАН НА ПОЧАТКУ ===
        var isCurrentlyOutage = MessageFormatter.IsCurrentlyInOutage(schedule.Today);
        var minutesFromMidnight = (int)(now - now.Date).TotalMinutes;

        if (isCurrentlyOutage)
        {
            var definiteSlots = schedule.Today.Slots.Where(s => s.Type == YasnoApi.Models.OutageSlotType.Definite).ToList();
            var currentSlot = definiteSlots.FirstOrDefault(s =>
                s.Start <= minutesFromMidnight && s.End > minutesFromMidnight);

            if (currentSlot != null)
            {
                var endTime = now.Date.Add(currentSlot.GetEndTime());
                var remaining = endTime - now;

                sb.AppendLine("┌─────────────────────────");
                sb.AppendLine("│ 🔴 <b>ЗАРАЗ ВІДКЛЮЧЕННЯ</b>");
                
                if (remaining.TotalHours >= 1)
                {
                    sb.AppendLine($"│ ⏳ Залишилось: <b>{remaining.Hours} год {remaining.Minutes} хв</b>");
                }
                else
                {
                    sb.AppendLine($"│ ⏳ Залишилось: <b>{remaining.Minutes} хв</b>");
                }
                
                sb.AppendLine($"│ 🕐 Світло до: <code>{endTime:HH:mm}</code>");
                sb.AppendLine("└─────────────────────────");
            }
        }
        else
        {
            var definiteSlots = schedule.Today.Slots.Where(s => s.Type == YasnoApi.Models.OutageSlotType.Definite).ToList();
            var nextSlot = definiteSlots.FirstOrDefault(s => s.Start > minutesFromMidnight);

            sb.AppendLine("┌─────────────────────────");
            sb.AppendLine("│ ✅ <b>ЗАРАЗ СВІТЛО Є</b>");

            if (nextSlot != null)
            {
                var startTime = now.Date.Add(nextSlot.GetStartTime());
                var timeUntil = startTime - now;

                if (timeUntil.TotalHours >= 1)
                {
                    sb.AppendLine($"│ ⏰ Відключення через: <b>{timeUntil.Hours} год {timeUntil.Minutes} хв</b>");
                }
                else if (timeUntil.TotalMinutes > 30)
                {
                    sb.AppendLine($"│ ⏰ Відключення через: <b>{timeUntil.Minutes} хв</b>");
                }
                else
                {
                    sb.AppendLine("│ ⚠ <b>Скоро відключення!</b>");
                    sb.AppendLine($"│    Через <b>{timeUntil.Minutes} хв</b>");
                }

                sb.AppendLine($"│ 🕐 О: <code>{startTime:HH:mm}</code>");
            }
            else
            {
                sb.AppendLine("│ 🎉 Сьогодні більше відключень немає!");
            }

            sb.AppendLine("└─────────────────────────");
        }

        sb.AppendLine();
        sb.AppendLine("📢 <b>📋 Розклад</b>");
        sb.AppendLine();

        sb.AppendLine("┌─ 📅 <b>СЬОГОДНІ</b> ─────────────");
        AppendDailyScheduleWithoutCurrentStatus(sb, schedule.Today);
        sb.AppendLine("└────────────────────────");
        sb.AppendLine();

        sb.AppendLine("┌─ 📅 <b>ЗАВТРА</b> ───────────────");
        AppendDailyScheduleWithoutCurrentStatus(sb, schedule.Tomorrow);
        sb.AppendLine("└────────────────────────");
        sb.AppendLine();

        sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━");
        sb.AppendLine($"🔄 <i>Оновлено: {schedule.UpdatedOn.ToLocalTime():HH:mm, dd.MM.yyyy}</i>");

        return sb.ToString();
    }

    private static void AppendDailyScheduleWithoutCurrentStatus(StringBuilder sb, YasnoApi.Models.DailyOutageSchedule schedule)
    {
        var statusEmoji = schedule.Status switch
        {
            YasnoApi.Models.OutageScheduleStatus.ScheduleApplies => "✅",
            YasnoApi.Models.OutageScheduleStatus.WaitingForSchedule => "⏳",
            YasnoApi.Models.OutageScheduleStatus.Unavailable => "❌",
            _ => "❓"
        };

        var statusText = schedule.Status switch
        {
            YasnoApi.Models.OutageScheduleStatus.ScheduleApplies => "Розклад діє",
            YasnoApi.Models.OutageScheduleStatus.WaitingForSchedule => "Очікується розклад",
            YasnoApi.Models.OutageScheduleStatus.Unavailable => "Розклад недоступний",
            _ => schedule.Status.ToString()
        };

        sb.AppendLine($"│ {statusEmoji} <i>{statusText}</i>");

        var definiteSlots = schedule.Slots.Where(s => s.Type == YasnoApi.Models.OutageSlotType.Definite).ToList();

        if (definiteSlots.Any())
        {
            sb.AppendLine("│");
            sb.AppendLine("│ 🔴 <b>Відключення:</b>");

            foreach (var slot in definiteSlots)
            {
                var start = slot.GetStartTime();
                var end = slot.GetEndTime();
                var duration = slot.Duration;
                var durationText = duration.TotalHours >= 1
                    ? $"{duration.TotalHours:F1} год"
                    : $"{duration.TotalMinutes:F0} хв";

                sb.AppendLine($"│   ⏰ <code>{start:hh\\:mm}</code> → <code>{end:hh\\:mm}</code>  ({durationText})");
            }

            var totalMinutes = definiteSlots.Sum(s => s.End - s.Start);
            var totalHours = totalMinutes / 60.0;
            var periodsText = definiteSlots.Count == 1 ? "період" :
                             definiteSlots.Count < 5 ? "періоди" : "періодів";

            sb.AppendLine("│");
            sb.AppendLine($"│ 📊 <b>Загалом:</b> {totalHours:F1} год • {definiteSlots.Count} {periodsText}");
        }
        else if (schedule.Status == YasnoApi.Models.OutageScheduleStatus.ScheduleApplies)
        {
            // Тільки якщо розклад діє - показуємо що відключень немає
            sb.AppendLine("│");
            sb.AppendLine("│ ✅ <b>Відключень немає</b>");
            sb.AppendLine("│ 💡 Світло буде цілий день!");
        }
    }
}

public class TodayCommandHandler : BaseCommandHandler
{
    public TodayCommandHandler(
        IYasnoApiClient yasnoApiClient,
        IOptions<YasnoAddressConfiguration> addressConfig,
        IMessageFormatterService messageFormatter,
        ILogger<TodayCommandHandler> logger,
        IBotDataService dataService,
        IScheduleCacheService cacheService)
        : base(yasnoApiClient, addressConfig, messageFormatter, logger, dataService, cacheService)
    {
    }

    public override string CommandName => "today";
    public override string Description => "Розклад на сьогодні";

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

        var sb = new StringBuilder();
        sb.AppendLine("⚡ <b>СЬОГОДНІ</b>");
        sb.AppendLine($"🕐 {DateTime.Now:dd.MM.yyyy}");
        sb.AppendLine();
        sb.AppendLine($"📍 Група: <code>{group}.{subgroup}</code> ({dsoName})");
        sb.AppendLine();

        MessageFormatter.AppendDailySchedule(sb, schedule.Today, true);

        return sb.ToString();
    }
}

public class TomorrowCommandHandler : BaseCommandHandler
{
    public TomorrowCommandHandler(
        IYasnoApiClient yasnoApiClient,
        IOptions<YasnoAddressConfiguration> addressConfig,
        IMessageFormatterService messageFormatter,
        ILogger<TomorrowCommandHandler> logger,
        IBotDataService dataService,
        IScheduleCacheService cacheService)
        : base(yasnoApiClient, addressConfig, messageFormatter, logger, dataService, cacheService)
    {
    }

    public override string CommandName => "tomorrow";
    public override string Description => "Розклад на завтра";

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

        var sb = new StringBuilder();
        sb.AppendLine("⚡ <b>ЗАВТРА</b>");
        sb.AppendLine($"🕐 {DateTime.Now.AddDays(1):dd.MM.yyyy}");
        sb.AppendLine();
        sb.AppendLine($"📍 Група: <code>{group}.{subgroup}</code> ({dsoName})");
        sb.AppendLine();

        MessageFormatter.AppendDailySchedule(sb, schedule.Tomorrow, false);

        return sb.ToString();
    }
}

public class GroupCommandHandler : BaseCommandHandler
{
    public GroupCommandHandler(
        IYasnoApiClient yasnoApiClient,
        IOptions<YasnoAddressConfiguration> addressConfig,
        IMessageFormatterService messageFormatter,
        ILogger<GroupCommandHandler> logger,
        IBotDataService dataService,
        IScheduleCacheService cacheService)
        : base(yasnoApiClient, addressConfig, messageFormatter, logger, dataService, cacheService)
    {
    }

    public override string CommandName => "group";
    public override string Description => "Інформація про вашу групу";

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

        return $"""
                📍 <b>ВАША ГРУПА</b>
                
                Група: <code>{group}.{subgroup}</code> ({dsoName})
                
                ⚡ Використовуйте команди:
                • /status - Поточний статус
                • /today - Розклад на сьогодні
                • /tomorrow - Розклад на завтра
                • /schedule - Повний розклад
                • /setgroup - Змінити групу
                """;
    }
}

public class MenuCommandHandler : ICommandHandler
{
    public string CommandName => "menu";
    public string Description => "📋 Головне меню";

    public Task<string> HandleAsync(Message message, CancellationToken cancellationToken)
    {
        return Task.FromResult("📋 <b>ГОЛОВНЕ МЕНЮ</b>\n\nОберіть дію:");
    }
}

public class StartCommandHandler : ICommandHandler
{
    private readonly IBotDataService _dataService;

    public StartCommandHandler(IBotDataService dataService)
    {
        _dataService = dataService;
    }

    public string CommandName => "start";
    public string Description => "Початок роботи";

    public async Task<string> HandleAsync(Message message, CancellationToken cancellationToken)
    {
        // Перевіряємо чи є користувач з групою
        var users = await _dataService.GetActiveUsersAsync(cancellationToken);
        var user = users.FirstOrDefault(u => u.TelegramUserId == message.From!.Id);

        var hasGroup = user != null && !string.IsNullOrEmpty(user.GroupName) && user.DsoId.HasValue;

        if (hasGroup)
        {
            var dsoName = user!.DsoId == 301 ? "ДТЕК" : "ЦЕК";
            return $"""
                   👋 <b>Вітаю!</b>
                   
                   📍 Ваша група: <code>{user.GroupName}</code> ({dsoName})
                   
                   ⚡ <b>Доступні команди:</b>
                   • /status - Поточний статус світла
                   • /today - Розклад на сьогодні
                   • /tomorrow - Розклад на завтра
                   • /schedule - Повний розклад
                   • /setgroup - Змінити групу
                   
                   📋 Або використовуйте меню нижче.
                   """;
        }
        else
        {
            return """
                   👋 <b>Вітаю!</b>
                   
                   Я бот для моніторингу електропостачання в Україні.
                   
                   ⚡ <b>Можливості:</b>
                   • Відстеження статусу світла
                   • Перегляд розкладу відключень
                   • Автоматичні сповіщення про зміни
                   • Перегляд інших груп
                   
                   📍 <b>Почніть з вибору вашої групи!</b>
                   Натисніть кнопку <b>"📍 Моя група"</b> нижче або використайте команду /setgroup
                   
                   📋 Використовуйте меню нижче для роботи з ботом.
                   """;
        }
    }
}
