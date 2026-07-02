using System.Text.Json.Serialization;

namespace AVE.ElectricityBot.YasnoApi.Models;

/// <summary>
/// Статус розкладу відключень
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum OutageScheduleStatus
{
    /// <summary>
    /// Розклад застосовується
    /// </summary>
    ScheduleApplies,
    
    /// <summary>
    /// Очікування розкладу
    /// </summary>
    WaitingForSchedule,
    
    /// <summary>
    /// Розклад недоступний
    /// </summary>
    Unavailable,
    
    /// <summary>
    /// Аварійні відключення (графік не діє)
    /// </summary>
    EmergencyShutdowns,

    /// <summary>
    /// Відключень немає
    /// </summary>
    NoOutages
}
