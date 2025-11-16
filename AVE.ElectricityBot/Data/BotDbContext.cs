using AVE.ElectricityBot.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace AVE.ElectricityBot.Data;

/// <summary>
/// DbContext для Azure Cosmos DB
/// </summary>
public class BotDbContext : DbContext
{
    public BotDbContext(DbContextOptions<BotDbContext> options) : base(options)
    {
    }

    /// <summary>
    /// Історія розкладів (зберігаємо раз на годину)
    /// </summary>
    public DbSet<ScheduleHistory> ScheduleHistory { get; set; }

    /// <summary>
    /// Користувачі бота
    /// </summary>
    public DbSet<BotUser> Users { get; set; }

    /// <summary>
    /// Стан скрапінгу каналів (в тому ж контейнері users, але з іншим partition key)
    /// </summary>
    public DbSet<ChannelScrapingState> ChannelScrapingStates { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Конфігурація для Cosmos DB
        modelBuilder.Entity<ScheduleHistory>()
            .ToContainer("schedule_history")
            .HasPartitionKey(e => e.PartitionKey);

        modelBuilder.Entity<BotUser>()
            .ToContainer("users")
            .HasPartitionKey(e => e.PartitionKey);

        // Стан скрапінгу каналів - в тому ж контейнері "users", але з іншим partition key
        modelBuilder.Entity<ChannelScrapingState>()
            .ToContainer("users")
            .HasPartitionKey(e => e.PartitionKey);

        // Вкладені об'єкти
        modelBuilder.Entity<BotUser>()
            .OwnsOne(u => u.Address);

        modelBuilder.Entity<BotUser>()
            .OwnsOne(u => u.Settings);
    }
}
