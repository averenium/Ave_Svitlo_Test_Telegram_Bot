using System.ComponentModel.DataAnnotations;

namespace AVE.ElectricityBot.Data.Entities;

/// <summary>
/// Історія розкладу відключень (зберігається раз на годину)
/// </summary>
public class ScheduleHistory
{
    /// <summary>
    /// Унікальний ID документа (для Cosmos DB)
    /// </summary>
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Дата та час запису (округлено до години)
    /// </summary>
    [Required]
    public DateTime RecordedAt { get; set; }

    /// <summary>
    /// Дата, для якої зберігається розклад
    /// </summary>
    [Required]
    public DateTime ScheduleDate { get; set; }

    /// <summary>
    /// Загальна тривалість відключень (в хвилинах)
    /// </summary>
    [Required]
    public int TotalOutageMinutes { get; set; }

    /// <summary>
    /// Кількість періодів відключення
    /// </summary>
    [Required]
    public int OutagePeriods { get; set; }

    /// <summary>
    /// Статус розкладу
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string Status { get; set; } // "ScheduleApplies", "WaitingForSchedule", "Unavailable"

    /// <summary>
    /// Деталі слотів відключень (JSON)
    /// Формат: [{"start": 420, "end": 660, "type": "Definite"}, ...]
    /// </summary>
    [MaxLength(2000)]
    public string SlotsJson { get; set; }

    /// <summary>
    /// Група відключень (наприклад: "5.1")
    /// </summary>
    [MaxLength(10)]
    public string GroupName { get; set; }

    /// <summary>
    /// Partition key для Cosmos DB (за датою для ефективних запитів)
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string PartitionKey { get; set; } // Format: "yyyy-MM"
}
