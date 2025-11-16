using AVE.ElectricityBot.YasnoApi.Models;
using System.Text;

namespace AVE.ElectricityBot.Services.Interfaces;

/// <summary>
/// Сервіс для форматування повідомлень
/// </summary>
public interface IMessageFormatterService
{
    /// <summary>
    /// Перевірити чи зараз відключення
    /// </summary>
    bool IsCurrentlyInOutage(DailyOutageSchedule schedule);

    /// <summary>
    /// Отримати хеш розкладу для виявлення змін
    /// </summary>
    string GetScheduleHash(GroupOutageSchedule schedule);

    /// <summary>
    /// Додати денний розклад до StringBuilder
    /// </summary>
    void AppendDailySchedule(StringBuilder sb, DailyOutageSchedule schedule, bool isToday);

    /// <summary>
    /// Додати спрощений денний розклад до StringBuilder
    /// </summary>
    void AppendDailyScheduleSimple(StringBuilder sb, DailyOutageSchedule schedule);
}
