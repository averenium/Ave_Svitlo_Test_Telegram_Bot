using System.Text.Json.Serialization;

namespace AVE.ElectricityBot.YasnoApi.Models;

/// <summary>
/// Слот відключення електроенергії
/// </summary>
public class OutageSlot
{
    /// <summary>
    /// Початок слоту в хвилинах від початку дня (0-1440)
    /// </summary>
    [JsonPropertyName("start")]
    public int Start { get; set; }

    /// <summary>
    /// Кінець слоту в хвилинах від початку дня (0-1440)
    /// </summary>
    [JsonPropertyName("end")]
    public int End { get; set; }

    /// <summary>
    /// Тип відключення
    /// </summary>
    [JsonPropertyName("type")]
    public OutageSlotType Type { get; set; }

    /// <summary>
    /// Конвертує хвилини з початку дня в TimeSpan
    /// </summary>
    public TimeSpan GetStartTime() => TimeSpan.FromMinutes(Start);

    /// <summary>
    /// Конвертує хвилини з початку дня в TimeSpan
    /// </summary>
    public TimeSpan GetEndTime() => TimeSpan.FromMinutes(End);

    /// <summary>
    /// Тривалість слоту
    /// </summary>
    public TimeSpan Duration => TimeSpan.FromMinutes(End - Start);
}
