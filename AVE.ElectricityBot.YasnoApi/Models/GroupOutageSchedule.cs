using System.Text.Json.Serialization;

namespace AVE.ElectricityBot.YasnoApi.Models;

/// <summary>
/// Розклад відключень для групи (сьогодні та завтра)
/// </summary>
public class GroupOutageSchedule
{
    /// <summary>
    /// Розклад на сьогодні
    /// </summary>
    [JsonPropertyName("today")]
    public DailyOutageSchedule Today { get; set; } = new();

    /// <summary>
    /// Розклад на завтра
    /// </summary>
    [JsonPropertyName("tomorrow")]
    public DailyOutageSchedule Tomorrow { get; set; } = new();

    [JsonIgnore]
    public DailyOutageSchedule Current => DateTime.Now.Date == Today.Date.Date ? Today : Tomorrow;

    /// <summary>
    /// Час останнього оновлення
    /// </summary>
    [JsonPropertyName("updatedOn")]
    public DateTime UpdatedOn { get; set; }
}
