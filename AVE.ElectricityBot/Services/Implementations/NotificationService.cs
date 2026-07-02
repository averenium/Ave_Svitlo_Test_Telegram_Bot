using AVE.ElectricityBot.Configuration;
using AVE.ElectricityBot.Data.Entities;
using AVE.ElectricityBot.Data.Abstractions;
using AVE.ElectricityBot.Services.Interfaces;
using AVE.ElectricityBot.YasnoApi;
using AVE.ElectricityBot.YasnoApi.Models;
using Microsoft.Extensions.Options;
using System.Text;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace AVE.ElectricityBot.Services.Implementations;

/// <summary>
/// Сервіс для відправки автоматичних сповіщень про зміни електропостачання
/// </summary>
public class NotificationService : INotificationService
{
    private readonly ILogger<NotificationService> _logger;
    private readonly ITelegramBotClient _botClient;
    private readonly IScheduleCacheService _cacheService;
    private readonly IMessageFormatterService _messageFormatter;
    private readonly IKeyboardService _keyboardService;
    private readonly IBotDataService _dataService;

    // Стан для відстеження змін (по групах)
    private readonly Dictionary<string, GroupNotificationState> _groupStates = new();

    public NotificationService(
        ILogger<NotificationService> logger,
        ITelegramBotClient botClient,
        IScheduleCacheService cacheService,
        IMessageFormatterService messageFormatter,
        IKeyboardService keyboardService,
        IBotDataService dataService)
    {
        _logger = logger;
        _botClient = botClient;
        _cacheService = cacheService;
        _messageFormatter = messageFormatter;
        _keyboardService = keyboardService;
        _dataService = dataService;
    }

    private class GroupNotificationState
    {
        public DateTime? LastScheduleUpdate { get; set; }
        public bool? LastOutageStatus { get; set; }
        public string? LastScheduleHash { get; set; }
    }

    public async Task CheckAndSendNotificationAsync(bool forceStartup, CancellationToken cancellationToken)
    {
        try
        {
            // Отримуємо всіх активних користувачів з встановленою групою
            var users = await _dataService.GetActiveUsersAsync(cancellationToken);
            var usersWithGroups = users.Where(u => !string.IsNullOrEmpty(u.GroupName)).ToList();

            if (!usersWithGroups.Any())
            {
                _logger.LogInformation("Немає користувачів з встановленою групою");
                return;
            }

            _logger.LogInformation("📊 Перевірка для {Count} користувачів", usersWithGroups.Count);

            // 🔥 ОПТИМІЗАЦІЯ: Групуємо користувачів по DSO
            var usersByDso = usersWithGroups
                .Where(u => u.DsoId.HasValue)
                .GroupBy(u => u.DsoId!.Value)
                .ToList();

            if (!usersByDso.Any())
            {
                _logger.LogWarning("Немає користувачів з встановленим DSO");
                return;
            }

            _logger.LogInformation("📍 Обробка {DsoCount} різних DSO", usersByDso.Count);

            // Обробляємо кожен DSO окремо
            foreach (var dsoUsers in usersByDso)
            {
                var dsoId = dsoUsers.Key;
                var dsoName = dsoId == 301 ? "ДТЕК" : "ЦЕК";

                _logger.LogInformation("⚡ Обробка DSO: {DsoName} (користувачів: {Count})", 
                    dsoName, dsoUsers.Count());

                // Отримуємо розклад для цього DSO (з кешу)
                var allOutages = await _cacheService.GetPlannedOutagesAsync(dsoId, cancellationToken);
                if (allOutages == null)
                {
                    _logger.LogWarning("Не вдалося отримати розклад для DSO={DsoId}", dsoId);
                    continue;
                }

                // Групуємо користувачів цього DSO по групах
                var usersByGroup = dsoUsers.GroupBy(u => u.GroupName).ToList();

                _logger.LogInformation("📍 Обробка {GroupCount} різних груп для {DsoName}", 
                    usersByGroup.Count, dsoName);

                foreach (var groupUsers in usersByGroup)
                {
                    var groupName = groupUsers.Key!;
                    var parts = groupName.Split('.');
                    
                    if (parts.Length != 2 || 
                        !int.TryParse(parts[0], out var group) || 
                        !int.TryParse(parts[1], out var subgroup))
                    {
                        _logger.LogWarning("Невірний формат групи: {GroupName}", groupName);
                        continue;
                    }

                    try
                    {
                        await ProcessGroupNotificationsAsync(
                            group, 
                            subgroup,
                            dsoId,
                            groupUsers.ToList(), 
                            allOutages,
                            forceStartup, 
                            cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Помилка обробки групи {GroupName} DSO={DsoId}", groupName, dsoId);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Помилка при обробці сповіщень");
        }
    }

    private async Task ProcessGroupNotificationsAsync(
        int group,
        int subgroup,
        int dsoId,
        List<Data.Entities.BotUser> users,
        PlannedOutagesResponse allOutages,
        bool forceStartup,
        CancellationToken cancellationToken)
    {
        var groupKey = $"{dsoId}_{group}.{subgroup}"; // Ключ тепер включає DSO
        
        // Отримуємо розклад для групи з загального об'єкта
        var schedule = allOutages.GetSchedule(group, subgroup);
        
        if (schedule == null)
        {
            _logger.LogWarning("Розклад для групи {Group} DSO={DsoId} не знайдено", $"{group}.{subgroup}", dsoId);
            return;
        }

        // Зберігаємо історію (раз на годину)
        if (await _dataService.ShouldSaveNewRecordAsync(cancellationToken))
        {
            await SaveScheduleHistoryAsync(group, subgroup, dsoId, schedule, cancellationToken);
        }

        // Отримуємо або створюємо стан для групи
        if (!_groupStates.ContainsKey(groupKey))
        {
            _groupStates[groupKey] = new GroupNotificationState();
        }

        var state = _groupStates[groupKey];

        // Перевіряємо чи потрібно відправляти
        var (shouldSend, reason) = ShouldSendNotification(schedule, state, forceStartup);

        if (shouldSend)
        {
            var dsoName = dsoId == 301 ? "ДТЕК" : "ЦЕК";
            _logger.LogInformation("📤 Відправка для групи {Group} ({DsoName}): {Reason} ({Count} користувачів)", 
                $"{group}.{subgroup}", dsoName, reason, users.Count);

            foreach (var user in users)
            {
                try
                {
                    if (!forceStartup || user.TelegramChatId == 395533392)
                        await SendNotificationToUserAsync(
                        user, 
                        group, 
                        subgroup,
                        dsoId,
                        schedule, 
                        reason, 
                        cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Помилка відправки користувачу {UserId}", user.TelegramUserId);
                }
            }

            UpdateGroupState(state, schedule);
        }
        else
        {
            InitializeStateIfNeeded(state, schedule);
        }
    }

    private (bool shouldSend, string reason) ShouldSendNotification(
        GroupOutageSchedule schedule,
        GroupNotificationState state,
        bool forceStartup)
    {
        if (forceStartup)
        {
            return (true, "🚀 Запуск");
        }

        // Отримуємо поточний хеш розкладу
        var currentScheduleHash = _messageFormatter.GetScheduleHash(schedule);

        // Перевірка зміни розкладу (найважливіша перевірка - реальні зміни даних)
        if (!string.IsNullOrEmpty(state.LastScheduleHash) && currentScheduleHash != state.LastScheduleHash)
        {
            _logger.LogInformation("📅 Розклад змінився: старий хеш={OldHash}, новий хеш={NewHash}", 
                state.LastScheduleHash, currentScheduleHash);
            return (true, "📅 Змінено розклад");
        }

        // Перевірка зміни статусу (світло є/немає) - перевіряємо тільки якщо розклад не змінився
        var currentOutageStatus = _messageFormatter.IsCurrentlyInOutage(schedule.Current);
        if (state.LastOutageStatus.HasValue && currentOutageStatus != state.LastOutageStatus.Value)
        {
            var reason = currentOutageStatus ? "🔴 Відключення" : "✅ Світло повернули";
            _logger.LogInformation("⚡ Статус змінився: {Reason}", reason);
            return (true, reason);
        }

        // Якщо нічого не змінилось - не відправляємо
        return (false, string.Empty);
    }

    private async Task SendNotificationToUserAsync(
        Data.Entities.BotUser user,
        int group,
        int subgroup,
        int dsoId,
        GroupOutageSchedule schedule,
        string reason,
        CancellationToken cancellationToken)
    {
        var message = BuildNotificationMessage(group, subgroup, dsoId, schedule, reason);

        await _botClient.SendMessage(
            chatId: user.TelegramChatId.ToString(),
            text: message,
            parseMode: ParseMode.Html,
            replyMarkup: _keyboardService.GetMainMenu(),
            cancellationToken: cancellationToken);

        var dsoName = dsoId == 301 ? "ДТЕК" : "ЦЕК";
        _logger.LogInformation("📤 Повідомлення для @{Username} (група {Group}.{Subgroup}, {DsoName}): {Reason}", 
            user.Username ?? $"User{user.TelegramUserId}", 
            group, 
            subgroup,
            dsoName,
            reason);
    }

    private string BuildNotificationMessage(
        int group,
        int subgroup,
        int dsoId,
        GroupOutageSchedule schedule,
        string reason)
    {
        var sb = new StringBuilder();
        var dsoName = dsoId == 301 ? "ДТЕК" : "ЦЕК";
        var now = DateTime.Now;

        // Визначаємо поточний стан
        var isCurrentlyOutage = _messageFormatter.IsCurrentlyInOutage(schedule.Today);
        var minutesFromMidnight = (int)(now - now.Date).TotalMinutes;

        sb.AppendLine("⚡ <b>ЕЛЕКТРОПОСТАЧАННЯ</b>");
        sb.AppendLine($"🕐 {now:HH:mm:ss} • {now:dd.MM.yyyy}");
        sb.AppendLine($"📍 Група: <code>{group}.{subgroup}</code> ({dsoName})");
        sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━");
        sb.AppendLine();

        // === ПОТОЧНИЙ СТАН НА ПОЧАТКУ ===
        if (isCurrentlyOutage)
        {
            var definiteSlots = schedule.Today.Slots.Where(s => s.Type == OutageSlotType.Definite).ToList();
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
            var definiteSlots = schedule.Today.Slots.Where(s => s.Type == OutageSlotType.Definite).ToList();
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

        // Причина сповіщення (якщо є)
        if (!reason.StartsWith("🚀"))
        {
            sb.AppendLine($"📢 <b>{reason}</b>");
            sb.AppendLine();
        }

        // === ДЕТАЛЬНИЙ РОЗКЛАД ===
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

    private void AppendDailyScheduleWithoutCurrentStatus(StringBuilder sb, DailyOutageSchedule schedule)
    {
        var statusEmoji = schedule.Status switch
        {
            OutageScheduleStatus.ScheduleApplies => "✅",
            OutageScheduleStatus.WaitingForSchedule => "⏳",
            OutageScheduleStatus.Unavailable => "❌",
            OutageScheduleStatus.EmergencyShutdowns => "🚨",
            OutageScheduleStatus.NoOutages => "✅",
            _ => "❓"
        };

        var statusText = schedule.Status switch
        {
            OutageScheduleStatus.ScheduleApplies => "Розклад діє",
            OutageScheduleStatus.WaitingForSchedule => "Очікується розклад",
            OutageScheduleStatus.Unavailable => "Розклад недоступний",
            OutageScheduleStatus.EmergencyShutdowns => "Аварійні відключення",
            OutageScheduleStatus.NoOutages => "Відключень немає",
            _ => schedule.Status.ToString()
        };

        sb.AppendLine($"│ {statusEmoji} <i>{statusText}</i>");

        var definiteSlots = schedule.Slots.Where(s => s.Type == OutageSlotType.Definite).ToList();

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
                             definiteSlots.Count < 5 ? "періоди" : "períодів";

            sb.AppendLine("│");
            sb.AppendLine($"│ 📊 <b>Загалом:</b> {totalHours:F1} год • {definiteSlots.Count} {periodsText}");
        }
        else if (schedule.Status == OutageScheduleStatus.ScheduleApplies
              || schedule.Status == OutageScheduleStatus.NoOutages)
        {
            sb.AppendLine("│");
            sb.AppendLine("│ ✅ <b>Відключень немає</b>");
            sb.AppendLine("│ 💡 Світло буде цілий день!");
        }
        else if (schedule.Status == OutageScheduleStatus.EmergencyShutdowns)
        {
            sb.AppendLine("│");
            sb.AppendLine("│ 🚨 <b>Розклад не діє</b>");
            sb.AppendLine("│ ⚠️ Інформація про відключення відсутня");
        }
    }

    private void UpdateGroupState(GroupNotificationState state, GroupOutageSchedule schedule)
    {
        state.LastScheduleUpdate = schedule.UpdatedOn;
        state.LastOutageStatus = _messageFormatter.IsCurrentlyInOutage(schedule.Today);
        state.LastScheduleHash = _messageFormatter.GetScheduleHash(schedule);
    }

    private void InitializeStateIfNeeded(GroupNotificationState state, GroupOutageSchedule schedule)
    {
        if (!state.LastScheduleUpdate.HasValue)
        {
            UpdateGroupState(state, schedule);
            _logger.LogInformation("Стан групи ініціалізовано");
        }
    }

    private async Task SaveScheduleHistoryAsync(
        int group,
        int subgroup,
        int dsoId,
        GroupOutageSchedule schedule,
        CancellationToken cancellationToken)
    {
        try
        {
            // Зберігаємо сьогодні
            await SaveDailyScheduleAsync(group, subgroup, dsoId, schedule.Today, cancellationToken);
            
            // Зберігаємо завтра
            await SaveDailyScheduleAsync(group, subgroup, dsoId, schedule.Tomorrow, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Помилка при збереженні історії розкладу");
        }
    }

    private async Task SaveDailyScheduleAsync(
        int group,
        int subgroup,
        int dsoId,
        DailyOutageSchedule schedule,
        CancellationToken cancellationToken)
    {
        var definiteSlots = schedule.Slots
            .Where(s => s.Type == OutageSlotType.Definite)
            .ToList();

        var totalMinutes = definiteSlots.Sum(s => s.End - s.Start);
        var slotsJson = System.Text.Json.JsonSerializer.Serialize(
            definiteSlots.Select(s => new { s.Start, s.End, Type = s.Type.ToString() }));

        var dsoName = dsoId == 301 ? "ДТЕК" : "ЦЕК";

        await _dataService.SaveScheduleHistoryAsync(
            scheduleDate: schedule.Date,
            totalOutageMinutes: totalMinutes,
            outagePeriods: definiteSlots.Count,
            status: schedule.Status.ToString(),
            slotsJson: slotsJson,
            groupName: $"{group}.{subgroup} ({dsoName})",
            cancellationToken: cancellationToken);
    }
}
