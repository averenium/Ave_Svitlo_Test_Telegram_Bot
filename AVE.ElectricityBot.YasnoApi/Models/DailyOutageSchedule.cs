using System.Text.Json.Serialization;

namespace AVE.ElectricityBot.YasnoApi.Models;

/// <summary>
/// Розклад відключень на день
/// </summary>
public class DailyOutageSchedule
{
    /// <summary>
    /// Слоти відключень
    /// </summary>
    [JsonPropertyName("slots")]
    public List<OutageSlot> Slots { get; set; } = new();

    /// <summary>
    /// Дата розкладу
    /// </summary>
    [JsonPropertyName("date")]
    public DateTime Date { get; set; }

    /// <summary>
    /// Статус розкладу
    /// </summary>
    [JsonPropertyName("status")]
    public OutageScheduleStatus Status { get; set; }
}
