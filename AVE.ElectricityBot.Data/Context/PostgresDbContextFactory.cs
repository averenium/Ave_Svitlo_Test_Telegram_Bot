using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AVE.ElectricityBot.Data.Context;

/// <summary>
/// Фабрика для створення PostgresDbContext в Design-time (для міграцій)
/// </summary>
public class PostgresDbContextFactory : IDesignTimeDbContextFactory<PostgresDbContext>
{
    public PostgresDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<PostgresDbContext>();
        
        // Використовуємо тимчасовий connection string для створення міграцій
        // Реальний connection string буде завантажено з appsettings.json в runtime
        optionsBuilder.UseNpgsql("Host=localhost;Port=5432;Database=electricity_bot;Username=postgres;Password=password")
            .UseSnakeCaseNamingConvention();

        return new PostgresDbContext(optionsBuilder.Options);
    }
}
