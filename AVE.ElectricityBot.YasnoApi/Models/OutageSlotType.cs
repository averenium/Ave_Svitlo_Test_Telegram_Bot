using System.Text.Json.Serialization;

namespace AVE.ElectricityBot.YasnoApi.Models;

/// <summary>
/// Тип слоту відключення
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum OutageSlotType
{
    /// <summary>
    /// Визначене відключення
    /// </summary>
    Definite,
    
    /// <summary>
    /// Відключення не заплановане
    /// </summary>
    NotPlanned,
    
    /// <summary>
    /// Можливе відключення
    /// </summary>
    Maybe
}
