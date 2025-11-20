using AVE.ElectricityBot.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace AVE.ElectricityBot.Data.Context;

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

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);
        
        // Вимикаємо конвенцію для індексів в CosmosDB
        // CosmosDB не підтримує Index атрибути в EF Core provider
        configurationBuilder.Conventions.Remove(typeof(Microsoft.EntityFrameworkCore.Metadata.Conventions.IndexAttributeConvention));
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Конфігурація для Cosmos DB - ScheduleHistory в окремому контейнері
        modelBuilder.Entity<ScheduleHistory>()
            .ToContainer("schedule_history")
            .HasPartitionKey(e => e.PartitionKey)
            .HasNoDiscriminator(); // Один тип в контейнері - не потрібен discriminator

        // BotUser і ChannelScrapingState в одному контейнері - потрібен discriminator
        // Використовуємо "$type" щоб відповідати існуючим даним в CosmosDB
        modelBuilder.Entity<BotUser>()
            .ToContainer("users")
            .HasPartitionKey(e => e.PartitionKey)
            .HasDiscriminator<string>("$type") // Використовуємо "$type" як в CosmosDB
            .HasValue<BotUser>("BotUser"); // Значення для BotUser

        modelBuilder.Entity<ChannelScrapingState>()
            .ToContainer("users")
            .HasPartitionKey(e => e.PartitionKey)
            .HasDiscriminator<string>("$type") // Той самий discriminator "$type"
            .HasValue<ChannelScrapingState>("ChannelScrapingState"); // Значення для ChannelScrapingState

        // Вкладені об'єкти
        modelBuilder.Entity<BotUser>()
            .OwnsOne(u => u.Address);

        modelBuilder.Entity<BotUser>()
            .OwnsOne(u => u.Settings);
    }
}
