using System.Text.Json.Serialization;

namespace AVE.ElectricityBot.YasnoApi.Models;

/// <summary>
/// Відповідь API Yasno з інформацією про групу відключень
/// </summary>
public class YasnoGroupResponse
{
    /// <summary>
    /// Номер групи відключень
    /// </summary>
    [JsonPropertyName("group")]
    public int Group { get; set; }

    /// <summary>
    /// Номер підгрупи
    /// </summary>
    [JsonPropertyName("subgroup")]
    public int Subgroup { get; set; }
}
