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
}
