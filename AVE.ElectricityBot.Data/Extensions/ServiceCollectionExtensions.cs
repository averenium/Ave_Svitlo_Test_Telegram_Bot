using AVE.ElectricityBot.Data.Abstractions;
using AVE.ElectricityBot.Data.Configuration;
using AVE.ElectricityBot.Data.Context;
using AVE.ElectricityBot.Data.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;

namespace AVE.ElectricityBot.Data.Extensions;

/// <summary>
/// Розширення для реєстрації сервісів роботи з БД
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Додає PostgreSQL DbContext з конфігурацією
    /// </summary>
    public static IServiceCollection AddPostgresData(
        this IServiceCollection services,
        string sectionName = "Postgres")
    {
        // Реєструємо конфігурацію PostgreSQL з секції Postgres
        services.AddOptions<PostgresConfiguration>()
            .BindConfiguration(sectionName);

        // Реєструємо NpgsqlDataSource як Singleton
        services.AddSingleton(serviceProvider =>
        {
            var pgConfig = serviceProvider.GetRequiredService<IOptions<PostgresConfiguration>>().Value;
            var connectionString = pgConfig.GetConnectionString();

            var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
            dataSourceBuilder.EnableDynamicJson();
            
            return dataSourceBuilder.Build();
        });

        // Реєструємо DbContext як Singleton, використовуючи DataSource
        services.AddDbContext<PostgresDbContext>((serviceProvider, options) =>
        {
            var dataSource = serviceProvider.GetRequiredService<NpgsqlDataSource>();

            options.UseNpgsql(dataSource)
                .UseSnakeCaseNamingConvention();
        }, ServiceLifetime.Singleton);

        services.AddSingleton<IBotDataService, BotDataService>();

        return services;
    }


    /// <summary>
    /// Додає PostgreSQL з можливістю налаштування через делегат
    /// </summary>
    public static IServiceCollection AddPostgresData(
        this IServiceCollection services,
        string connectionString,
        Action<DbContextOptionsBuilder>? configureOptions = null)
    {
        // Реєструємо NpgsqlDataSource
        services.AddSingleton(serviceProvider =>
        {
            var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
            dataSourceBuilder.EnableDynamicJson();
            
            return dataSourceBuilder.Build();
        });

        services.AddDbContext<PostgresDbContext>((serviceProvider, options) =>
        {
            var dataSource = serviceProvider.GetRequiredService<NpgsqlDataSource>();

            options.UseNpgsql(dataSource)
                .UseSnakeCaseNamingConvention();
            
            configureOptions?.Invoke(options);
        });

        services.AddScoped<IBotDataService, BotDataService>();

        return services;
    }
}
