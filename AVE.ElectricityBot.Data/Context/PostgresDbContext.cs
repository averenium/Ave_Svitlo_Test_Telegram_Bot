using AVE.ElectricityBot.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace AVE.ElectricityBot.Data.Context;

/// <summary>
/// DbContext для PostgreSQL / TimescaleDB
/// </summary>
public class PostgresDbContext : DbContext
{
    public PostgresDbContext(DbContextOptions<PostgresDbContext> options) : base(options)
    {
    }

    public DbSet<ScheduleHistory> ScheduleHistory { get; set; }
    public DbSet<BotUser> Users { get; set; }
    public DbSet<ChannelScrapingState> ChannelScrapingStates { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Налаштування BotUser owned entities як JSON (для автоматичного відстеження змін)
        modelBuilder.Entity<BotUser>(entity =>
        {
            // Settings зберігається як JSONB з автоматичним change tracking
            entity.OwnsOne(u => u.Settings, ownedNavigationBuilder =>
            {
                ownedNavigationBuilder.ToJson("settings");
            });

            // Address зберігається як JSONB з автоматичним change tracking
            entity.OwnsOne(u => u.Address, ownedNavigationBuilder =>
            {
                ownedNavigationBuilder.ToJson("address");
            });
        });
    }
}
