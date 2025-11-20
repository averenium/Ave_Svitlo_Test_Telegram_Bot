using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace AVE.ElectricityBot.Data.Entities;

/// <summary>
/// Стан скрапінгу каналу - зберігає ID останнього обробленого повідомлення
/// Зберігається в тому ж контейнері що й користувачі, але з іншим partition key
/// </summary>
[Index(nameof(ChannelUsername), IsUnique = true, Name = "ix_channel_scraping_channel_username")]
[Index(nameof(LastScrapeAt), Name = "ix_channel_scraping_last_scrape_at")]
public class ChannelScrapingState
{
    /// <summary>
    /// Унікальний ID документа (формат: "channel:{channelUsername}")
    /// </summary>
    [Key]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Username каналу (dnipro_svet, ddnepr_vse)
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string ChannelUsername { get; set; } = string.Empty;

    /// <summary>
    /// ID останнього обробленого повідомлення
    /// </summary>
    [Required]
    public long LastProcessedMessageId { get; set; }

    /// <summary>
    /// Дата і час останнього обробленого повідомлення
    /// </summary>
    [Required]
    public DateTime LastProcessedAt { get; set; }

    /// <summary>
    /// Дата і час останнього запуску скрапінгу
    /// </summary>
    [Required]
    public DateTime LastScrapeAt { get; set; }

    /// <summary>
    /// Кількість оброблених повідомлень за весь час
    /// </summary>
    [Required]
    public int TotalProcessedMessages { get; set; }

    /// <summary>
    /// Partition key для Cosmos DB (завжди "channel_state")
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string PartitionKey { get; set; } = "channel_state";
}
