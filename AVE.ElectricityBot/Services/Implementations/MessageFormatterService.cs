using AVE.ElectricityBot.Services.Interfaces;
using AVE.ElectricityBot.YasnoApi.Models;
using System.Text;

namespace AVE.ElectricityBot.Services.Implementations;

/// <summary>
/// Сервіс для форматування повідомлень
/// </summary>
public class MessageFormatterService : IMessageFormatterService
{
    public bool IsCurrentlyInOutage(DailyOutageSchedule schedule)
    {
        var now = DateTime.Now;
        var minutesFromMidnight = (int)(now - now.Date).TotalMinutes;
        return schedule.Slots.Where(s => s.Type == OutageSlotType.Definite)
            .Any(s => s.Start <= minutesFromMidnight && s.End > minutesFromMidnight);
    }

    public string GetScheduleHash(GroupOutageSchedule schedule)
    {
        var sb = new StringBuilder();
        
        // Додаємо дату сьогодні та статус до хешу
        sb.Append($"T:{schedule.Today.Date:yyyy-MM-dd}:{schedule.Today.Status}:");
        foreach (var slot in schedule.Today.Slots.Where(s => s.Type == OutageSlotType.Definite).OrderBy(s => s.Start))
            sb.Append($"{slot.Start}-{slot.End};");
        
        sb.Append("|");
        
        // Додаємо дату завтра та статус до хешу
        sb.Append($"T:{schedule.Tomorrow.Date:yyyy-MM-dd}:{schedule.Tomorrow.Status}:");
        foreach (var slot in schedule.Tomorrow.Slots.Where(s => s.Type == OutageSlotType.Definite).OrderBy(s => s.Start))
            sb.Append($"{slot.Start}-{slot.End};");
        
        return sb.ToString();
    }

    public void AppendDailyScheduleSimple(StringBuilder sb, DailyOutageSchedule schedule)
    {
        var definiteSlots = schedule.Slots.Where(s => s.Type == OutageSlotType.Definite).ToList();
        
        if (definiteSlots.Any())
        {
            foreach (var slot in definiteSlots)
            {
                var start = slot.GetStartTime();
                var end = slot.GetEndTime();
                var duration = slot.Duration;
                var durationText = duration.TotalHours >= 1 
                    ? $"{duration.TotalHours:F1} год" 
                    : $"{duration.TotalMinutes:F0} хв";
                
                sb.AppendLine($"│ ⏰ <code>{start:hh\\:mm}</code> → <code>{end:hh\\:mm}</code>  ({durationText})");
            }
            
            var totalMinutes = definiteSlots.Sum(s => s.End - s.Start);
            var totalHours = totalMinutes / 60.0;
            sb.AppendLine("│");
            sb.AppendLine($"│ 📊 Всього: {totalHours:F1} год");
        }
        else
        {
            sb.AppendLine("│ ✅ Відключень немає");
        }
    }

    public void AppendDailySchedule(StringBuilder sb, DailyOutageSchedule schedule, bool isToday)
    {
        var statusEmoji = GetStatusEmoji(schedule.Status);
        var statusText = GetStatusText(schedule.Status);
        sb.AppendLine($"│ {statusEmoji} <i>{statusText}</i>");
        sb.AppendLine("│");

        var definiteSlots = schedule.Slots.Where(s => s.Type == OutageSlotType.Definite).ToList();

        if (definiteSlots.Any())
        {
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

            if (isToday)
            {
                sb.AppendLine("│");
                AppendCurrentStatus(sb, definiteSlots);
            }
        }
        else if (schedule.Status == OutageScheduleStatus.ScheduleApplies
              || schedule.Status == OutageScheduleStatus.NoOutages)
        {
            sb.AppendLine("│ ✅ <b>Відключень немає</b>");
            sb.AppendLine("│ 💡 Світло буде цілий день!");
        }
        else if (schedule.Status == OutageScheduleStatus.EmergencyShutdowns)
        {
            sb.AppendLine("│ 🚨 <b>Розклад не діє</b>");
            sb.AppendLine("│ ⚠️ Інформація про відключення відсутня");
        }
    }

    private void AppendCurrentStatus(StringBuilder sb, List<OutageSlot> definiteSlots)
    {
        var now = DateTime.Now;
        var minutesFromMidnight = (int)(now - now.Date).TotalMinutes;

        var currentSlot = definiteSlots.FirstOrDefault(s =>
            s.Start <= minutesFromMidnight && s.End > minutesFromMidnight);

        if (currentSlot != null)
        {
            var endTime = now.Date.Add(currentSlot.GetEndTime());
            var remaining = endTime - now;

            sb.AppendLine("│ ┌─────────────────────");
            sb.AppendLine("│ │ 🚨 <b>ЗАРАЗ ВІДКЛЮЧЕННЯ!</b>");

            if (remaining.TotalHours >= 1)
            {
                sb.AppendLine($"│ │ ⏳ Залишилось: <b>{remaining.Hours} год {remaining.Minutes} хв</b>");
            }
            else
            {
                sb.AppendLine($"│ │ ⏳ Залишилось: <b>{remaining.Minutes} хв</b>");
            }

            sb.AppendLine($"│ │ 🕐 До: <code>{endTime:HH:mm}</code>");
            sb.AppendLine("│ └─────────────────────");
        }
        else
        {
            var nextSlot = definiteSlots.FirstOrDefault(s => s.Start > minutesFromMidnight);

            if (nextSlot != null)
            {
                var startTime = now.Date.Add(nextSlot.GetStartTime());
                var timeUntil = startTime - now;

                sb.AppendLine("│ ┌─────────────────────");
                sb.AppendLine("│ │ ✅ <b>Зараз світло є</b>");

                if (timeUntil.TotalHours >= 1)
                {
                    sb.AppendLine($"│ │ ⏰ Наступне відключення через:");
                    sb.AppendLine($"│ │    <b>{timeUntil.Hours} год {timeUntil.Minutes} хв</b>");
                }
                else if (timeUntil.TotalMinutes > 30)
                {
                    sb.AppendLine($"│ │ ⏰ Наступне відключення через:");
                    sb.AppendLine($"│ │    <b>{timeUntil.Minutes} хв</b>");
                }
                else
                {
                    sb.AppendLine("│ │ ⚠ <b>Скоро відключення!</b>");
                    sb.AppendLine($"│ │    Через <b>{timeUntil.Minutes} хв</b>");
                }

                sb.AppendLine($"│ │ 🕐 О: <code>{startTime:HH:mm}</code>");
                sb.AppendLine("│ └─────────────────────");
            }
            else
            {
                sb.AppendLine("│ ┌─────────────────────");
                sb.AppendLine("│ │ ✅ <b>Зараз світло є</b>");
                sb.AppendLine("│ │ 🎉 Більше відключень немає!");
                sb.AppendLine("│ └─────────────────────");
            }
        }
    }

    private static string GetStatusEmoji(OutageScheduleStatus status)
    {
        return status switch
        {
            OutageScheduleStatus.ScheduleApplies => "✅",
            OutageScheduleStatus.WaitingForSchedule => "⏳",
            OutageScheduleStatus.Unavailable => "❌",
            OutageScheduleStatus.EmergencyShutdowns => "🚨",
            OutageScheduleStatus.NoOutages => "✅",
            _ => "❓"
        };
    }

    private static string GetStatusText(OutageScheduleStatus status)
    {
        return status switch
        {
            OutageScheduleStatus.ScheduleApplies => "Розклад діє",
            OutageScheduleStatus.WaitingForSchedule => "Очікується розклад",
            OutageScheduleStatus.Unavailable => "Розклад недоступний",
            OutageScheduleStatus.EmergencyShutdowns => "Аварійні відключення",
            OutageScheduleStatus.NoOutages => "Відключень немає",
            _ => status.ToString()
        };
    }
}
