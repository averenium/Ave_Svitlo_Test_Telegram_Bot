using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace AVE.ElectricityBot.Data.Entities;

/// <summary>
/// Користувач бота
/// </summary>
[Index(nameof(TelegramUserId), IsUnique = true, Name = "ix_users_telegram_user_id")]
[Index(nameof(TelegramChatId), Name = "ix_users_telegram_chat_id")]
[Index(nameof(IsActive), Name = "ix_users_is_active")]
[Index(nameof(LastActiveAt), Name = "ix_users_last_active_at")]
[Index(nameof(GroupName), Name = "ix_users_group_name")]
[Index(nameof(DsoId), Name = "ix_users_dso_id")]
public class BotUser
{
    /// <summary>
    /// Унікальний ID документа (для Cosmos DB)
    /// </summary>
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Telegram User ID
    /// </summary>
    [Required]
    public long TelegramUserId { get; set; }

    /// <summary>
    /// Telegram Chat ID
    /// </summary>
    [Required]
    public long TelegramChatId { get; set; }

    /// <summary>
    /// Username в Telegram (@username)
    /// </summary>
    [MaxLength(100)]
    public string? Username { get; set; }

    /// <summary>
    /// Ім'я користувача
    /// </summary>
    [MaxLength(100)]
    public string? FirstName { get; set; }

    /// <summary>
    /// Прізвище користувача
    /// </summary>
    [MaxLength(100)]
    public string? LastName { get; set; }

    /// <summary>
    /// Група відключень користувача (наприклад: "5.1")
    /// </summary>
    [MaxLength(10)]
    public string? GroupName { get; set; }

    /// <summary>
    /// ID оператора DSO (301 - ДТЕК, 303 - ЦЕК)
    /// </summary>
    public int? DsoId { get; set; }

    /// <summary>
    /// Адреса користувача
    /// </summary>
    public UserAddress? Address { get; set; }

    /// <summary>
    /// Налаштування користувача
    /// </summary>
    public UserSettings? Settings { get; set; }

    /// <summary>
    /// Коли користувач вперше взаємодіяв з ботом
    /// </summary>
    [Required]
    public DateTime FirstSeenAt { get; set; }

    /// <summary>
    /// Остання активність користувача
    /// </summary>
    [Required]
    public DateTime LastActiveAt { get; set; }

    /// <summary>
    /// Кількість команд від користувача
    /// </summary>
    [Required]
    public int CommandsCount { get; set; }

    /// <summary>
    /// Чи активний користувач
    /// </summary>
    [Required]
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Partition key для Cosmos DB (використовуємо TelegramUserId для розподілу)
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string PartitionKey { get; set; } = "users";
}

/// <summary>
/// Адреса користувача (вкладений об'єкт)
/// </summary>
public class UserAddress
{
    public int? RegionId { get; set; }
    public int? CityId { get; set; }
    public int? StreetId { get; set; }
    public int? HouseId { get; set; }
    public int? DsoId { get; set; }
}

/// <summary>
/// Налаштування користувача (вкладений об'єкт)
/// </summary>
public class UserSettings
{
    /// <summary>
    /// Чи увімкнено отримання повідомлень з каналів Telegram
    /// </summary>
    public bool EnableChannelScraping { get; set; } = true;

    /// <summary>
    /// Чи увімкнено сповіщення про зміну статусу світла
    /// </summary>
    public bool EnableStatusNotifications { get; set; } = true;

    /// <summary>
    /// Чи увімкнено сповіщення про зміну розкладу
    /// </summary>
    public bool EnableScheduleNotifications { get; set; } = true;
}
