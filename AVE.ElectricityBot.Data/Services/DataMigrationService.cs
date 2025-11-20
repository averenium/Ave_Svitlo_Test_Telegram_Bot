using AVE.ElectricityBot.Data.Context;
using AVE.ElectricityBot.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AVE.ElectricityBot.Data.Services;

/// <summary>
/// Сервіс для міграції даних з CosmosDB в PostgreSQL
/// </summary>
public class DataMigrationService
{
    private readonly BotDbContext _cosmosContext;
    private readonly PostgresDbContext _postgresContext;
    private readonly ILogger<DataMigrationService> _logger;

    public DataMigrationService(
        BotDbContext cosmosContext,
        PostgresDbContext postgresContext,
        ILogger<DataMigrationService> logger)
    {
        _cosmosContext = cosmosContext ?? throw new ArgumentNullException(nameof(cosmosContext));
        _postgresContext = postgresContext ?? throw new ArgumentNullException(nameof(postgresContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Мігрувати всі дані з CosmosDB в PostgreSQL
    /// </summary>
    public async Task<MigrationResult> MigrateAllDataAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("🚀 Початок міграції даних з CosmosDB в PostgreSQL...");

        var result = new MigrationResult
        {
            StartedAt = DateTime.UtcNow
        };

        try
        {
            // 1. Мігрувати користувачів
            result.UsersResult = await MigrateUsersAsync(cancellationToken);

            // 2. Мігрувати стан скрапінгу каналів
            result.ChannelStatesResult = await MigrateChannelScrapingStatesAsync(cancellationToken);

            // 3. Мігрувати історію розкладів
            result.ScheduleHistoryResult = await MigrateScheduleHistoryAsync(cancellationToken);

            result.CompletedAt = DateTime.UtcNow;
            result.IsSuccess = true;
            result.Duration = result.CompletedAt - result.StartedAt;

            _logger.LogInformation("✅ Міграція завершена успішно за {Duration}", result.Duration);
            _logger.LogInformation("📊 Підсумок: Користувачів: {Users} (+{UsersNew}, ↻{UsersUpdated}), Станів каналів: {Channels} (+{ChannelsNew}, ↻{ChannelsUpdated}), Історій: {Histories} (+{HistoriesNew}, ↻{HistoriesUpdated})",
                result.UsersResult.Migrated + result.UsersResult.Updated, result.UsersResult.Migrated, result.UsersResult.Updated,
                result.ChannelStatesResult.Migrated + result.ChannelStatesResult.Updated, result.ChannelStatesResult.Migrated, result.ChannelStatesResult.Updated,
                result.ScheduleHistoryResult.Migrated + result.ScheduleHistoryResult.Updated, result.ScheduleHistoryResult.Migrated, result.ScheduleHistoryResult.Updated);

            return result;
        }
        catch (Exception ex)
        {
            result.CompletedAt = DateTime.UtcNow;
            result.IsSuccess = false;
            result.ErrorMessage = ex.Message;
            result.Duration = result.CompletedAt - result.StartedAt;

            _logger.LogError(ex, "❌ Помилка під час міграції даних");
            throw;
        }
    }

    /// <summary>
    /// Мігрувати користувачів
    /// </summary>
    public async Task<EntityMigrationResult> MigrateUsersAsync(CancellationToken cancellationToken = default)
    {
        var result = new EntityMigrationResult { EntityType = "BotUser" };

        try
        {
            _logger.LogInformation("👥 Починаємо міграцію користувачів...");

            // Отримуємо всіх користувачів з CosmosDB
            var cosmosUsers = await _cosmosContext.Users.ToListAsync(cancellationToken);
            result.Total = cosmosUsers.Count;

            _logger.LogInformation("📊 Знайдено {Count} користувачів у CosmosDB", cosmosUsers.Count);

            foreach (var cosmosUser in cosmosUsers)
            {
                try
                {
                    // Перевіряємо чи користувач вже є в PostgreSQL
                    var existingUser = await _postgresContext.Users
                        .FirstOrDefaultAsync(u => u.TelegramUserId == cosmosUser.TelegramUserId, cancellationToken);

                    if (existingUser != null)
                    {
                        // Оновлюємо існуючого користувача
                        existingUser.TelegramChatId = cosmosUser.TelegramChatId;
                        existingUser.Username = cosmosUser.Username;
                        existingUser.FirstName = cosmosUser.FirstName;
                        existingUser.LastName = cosmosUser.LastName;
                        existingUser.GroupName = cosmosUser.GroupName;
                        existingUser.DsoId = cosmosUser.DsoId;
                        existingUser.Address = cosmosUser.Address != null ? new UserAddress
                        {
                            RegionId = cosmosUser.Address.RegionId,
                            CityId = cosmosUser.Address.CityId,
                            StreetId = cosmosUser.Address.StreetId,
                            HouseId = cosmosUser.Address.HouseId,
                            DsoId = cosmosUser.Address.DsoId
                        } : null;
                        existingUser.Settings = cosmosUser.Settings != null ? new UserSettings
                        {
                            EnableChannelScraping = cosmosUser.Settings.EnableChannelScraping,
                            EnableStatusNotifications = cosmosUser.Settings.EnableStatusNotifications,
                            EnableScheduleNotifications = cosmosUser.Settings.EnableScheduleNotifications
                        } : new UserSettings();
                        existingUser.FirstSeenAt = cosmosUser.FirstSeenAt.ToUniversalTime();
                        existingUser.LastActiveAt = cosmosUser.LastActiveAt.ToUniversalTime();
                        existingUser.CommandsCount = cosmosUser.CommandsCount;
                        existingUser.IsActive = cosmosUser.IsActive;

                        await _postgresContext.SaveChangesAsync(cancellationToken);
                        
                        result.Updated++;
                        _logger.LogDebug("🔄 Користувач {Username} ({UserId}) оновлено", 
                            cosmosUser.Username ?? "Unknown", cosmosUser.TelegramUserId);
                        continue;
                    }

                    // Створюємо нового користувача для PostgreSQL
                    var pgUser = new BotUser
                    {
                        Id = Guid.NewGuid().ToString(),
                        TelegramUserId = cosmosUser.TelegramUserId,
                        TelegramChatId = cosmosUser.TelegramChatId,
                        Username = cosmosUser.Username,
                        FirstName = cosmosUser.FirstName,
                        LastName = cosmosUser.LastName,
                        GroupName = cosmosUser.GroupName,
                        DsoId = cosmosUser.DsoId,
                        Address = cosmosUser.Address != null ? new UserAddress
                        {
                            RegionId = cosmosUser.Address.RegionId,
                            CityId = cosmosUser.Address.CityId,
                            StreetId = cosmosUser.Address.StreetId,
                            HouseId = cosmosUser.Address.HouseId,
                            DsoId = cosmosUser.Address.DsoId
                        } : null,
                        Settings = cosmosUser.Settings != null ? new UserSettings
                        {
                            EnableChannelScraping = cosmosUser.Settings.EnableChannelScraping,
                            EnableStatusNotifications = cosmosUser.Settings.EnableStatusNotifications,
                            EnableScheduleNotifications = cosmosUser.Settings.EnableScheduleNotifications
                        } : new UserSettings(),
                        FirstSeenAt = cosmosUser.FirstSeenAt.ToUniversalTime(),
                        LastActiveAt = cosmosUser.LastActiveAt.ToUniversalTime(),
                        CommandsCount = cosmosUser.CommandsCount,
                        IsActive = cosmosUser.IsActive,
                        PartitionKey = "users"
                    };

                    _postgresContext.Users.Add(pgUser);
                    await _postgresContext.SaveChangesAsync(cancellationToken);

                    result.Migrated++;
                    _logger.LogDebug("✅ Користувач {Username} ({UserId}) мігровано", 
                        cosmosUser.Username ?? "Unknown", cosmosUser.TelegramUserId);
                }
                catch (Exception ex)
                {
                    result.Failed++;
                    _logger.LogError(ex, "❌ Помилка при міграції користувача {UserId}", cosmosUser.TelegramUserId);
                }
            }

            _logger.LogInformation("✅ Міграція користувачів завершена: {Migrated} створено, {Updated} оновлено, {Failed} помилок",
                result.Migrated, result.Updated, result.Failed);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Критична помилка при міграції користувачів");
            throw;
        }
    }

    /// <summary>
    /// Мігрувати стан скрапінгу каналів
    /// </summary>
    public async Task<EntityMigrationResult> MigrateChannelScrapingStatesAsync(CancellationToken cancellationToken = default)
    {
        var result = new EntityMigrationResult { EntityType = "ChannelScrapingState" };

        try
        {
            _logger.LogInformation("📡 Починаємо міграцію станів скрапінгу каналів...");

            // Отримуємо всі стани з CosmosDB
            var cosmosStates = await _cosmosContext.ChannelScrapingStates.ToListAsync(cancellationToken);
            result.Total = cosmosStates.Count;

            _logger.LogInformation("📊 Знайдено {Count} станів каналів у CosmosDB", cosmosStates.Count);

            foreach (var cosmosState in cosmosStates)
            {
                try
                {
                    // Перевіряємо чи стан вже є в PostgreSQL
                    var existingState = await _postgresContext.ChannelScrapingStates
                        .FirstOrDefaultAsync(s => s.ChannelUsername == cosmosState.ChannelUsername, cancellationToken);

                    if (existingState != null)
                    {
                        // Оновлюємо існуючий стан
                        existingState.LastProcessedMessageId = cosmosState.LastProcessedMessageId;
                        existingState.LastProcessedAt = cosmosState.LastProcessedAt.ToUniversalTime();
                        existingState.LastScrapeAt = cosmosState.LastScrapeAt.ToUniversalTime();
                        existingState.TotalProcessedMessages = cosmosState.TotalProcessedMessages;

                        await _postgresContext.SaveChangesAsync(cancellationToken);
                        
                        result.Updated++;
                        _logger.LogDebug("🔄 Стан каналу @{Channel} оновлено", cosmosState.ChannelUsername);
                        continue;
                    }

                    // Створюємо новий стан для PostgreSQL
                    var pgState = new ChannelScrapingState
                    {
                        Id = $"channel:{cosmosState.ChannelUsername}",
                        ChannelUsername = cosmosState.ChannelUsername,
                        LastProcessedMessageId = cosmosState.LastProcessedMessageId,
                        LastProcessedAt = cosmosState.LastProcessedAt.ToUniversalTime(),
                        LastScrapeAt = cosmosState.LastScrapeAt.ToUniversalTime(),
                        TotalProcessedMessages = cosmosState.TotalProcessedMessages,
                        PartitionKey = "channel_state"
                    };

                    _postgresContext.ChannelScrapingStates.Add(pgState);
                    await _postgresContext.SaveChangesAsync(cancellationToken);

                    result.Migrated++;
                    _logger.LogDebug("✅ Стан каналу @{Channel} мігровано", cosmosState.ChannelUsername);
                }
                catch (Exception ex)
                {
                    result.Failed++;
                    _logger.LogError(ex, "❌ Помилка при міграції стану каналу @{Channel}", cosmosState.ChannelUsername);
                }
            }

            _logger.LogInformation("✅ Міграція станів каналів завершена: {Migrated} створено, {Updated} оновлено, {Failed} помилок",
                result.Migrated, result.Updated, result.Failed);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Критична помилка при міграції станів каналів");
            throw;
        }
    }

    /// <summary>
    /// Мігрувати історію розкладів
    /// </summary>
    public async Task<EntityMigrationResult> MigrateScheduleHistoryAsync(CancellationToken cancellationToken = default)
    {
        var result = new EntityMigrationResult { EntityType = "ScheduleHistory" };

        try
        {
            _logger.LogInformation("📅 Починаємо міграцію історії розкладів...");

            // Отримуємо всю історію з CosmosDB
            var cosmosHistories = await _cosmosContext.ScheduleHistory.ToListAsync(cancellationToken);
            result.Total = cosmosHistories.Count;

            _logger.LogInformation("📊 Знайдено {Count} записів історії у CosmosDB", cosmosHistories.Count);

            foreach (var cosmosHistory in cosmosHistories)
            {
                var recordedAtUtc = DateTime.MinValue;
                var scheduleDateUtc = DateTime.MinValue;
                
                try
                {
                    recordedAtUtc = cosmosHistory.RecordedAt.ToUniversalTime();
                    scheduleDateUtc = cosmosHistory.ScheduleDate.Date.ToUniversalTime();
                    
                    // Перевіряємо чи запис вже є в PostgreSQL (за датою, часом та групою)
                    var existingHistory = await _postgresContext.ScheduleHistory
                        .FirstOrDefaultAsync(h => 
                            h.RecordedAt == recordedAtUtc &&
                            h.ScheduleDate.Date == scheduleDateUtc &&
                            h.GroupName == cosmosHistory.GroupName, 
                            cancellationToken);

                    if (existingHistory != null)
                    {
                        // Оновлюємо існуючий запис
                        existingHistory.TotalOutageMinutes = cosmosHistory.TotalOutageMinutes;
                        existingHistory.OutagePeriods = cosmosHistory.OutagePeriods;
                        existingHistory.Status = cosmosHistory.Status;
                        existingHistory.SlotsJson = cosmosHistory.SlotsJson;

                        await _postgresContext.SaveChangesAsync(cancellationToken);
                        
                        result.Updated++;
                        _logger.LogDebug("🔄 Запис історії для групи {GroupName} на {Date} оновлено", cosmosHistory.GroupName, scheduleDateUtc);
                        continue;
                    }

                    // Створюємо новий запис історії для PostgreSQL
                    var pgHistory = new ScheduleHistory
                    {
                        Id = Guid.NewGuid().ToString(),
                        RecordedAt = recordedAtUtc,
                        ScheduleDate = DateTime.SpecifyKind(scheduleDateUtc, DateTimeKind.Utc),
                        TotalOutageMinutes = cosmosHistory.TotalOutageMinutes,
                        OutagePeriods = cosmosHistory.OutagePeriods,
                        Status = cosmosHistory.Status,
                        SlotsJson = cosmosHistory.SlotsJson,
                        GroupName = cosmosHistory.GroupName,
                        PartitionKey = cosmosHistory.ScheduleDate.ToString("yyyy-MM")
                    };

                    _postgresContext.ScheduleHistory.Add(pgHistory);
                    await _postgresContext.SaveChangesAsync(cancellationToken);

                    result.Migrated++;
                    
                    if (result.Migrated % 100 == 0)
                    {
                        _logger.LogInformation("⏳ Прогрес: {Migrated}/{Total} записів історії мігровано", 
                            result.Migrated, result.Total);
                    }
                }
                catch (Exception ex)
                {
                    result.Failed++;
                    _logger.LogError(ex, "❌ Помилка при міграції історії {Date}", cosmosHistory.ScheduleDate);
                }
            }

            _logger.LogInformation("✅ Міграція історії завершена: {Migrated} створено, {Updated} оновлено, {Failed} помилок",
                result.Migrated, result.Updated, result.Failed);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Критична помилка при міграції історії розкладів");
            throw;
        }
    }

    /// <summary>
    /// Перевірити кількість записів у обох БД
    /// </summary>
    public async Task<MigrationStats> GetMigrationStatsAsync(CancellationToken cancellationToken = default)
    {
        var stats = new MigrationStats
        {
            CosmosUsersCount = await _cosmosContext.Users.CountAsync(cancellationToken),
            CosmosChannelStatesCount = await _cosmosContext.ChannelScrapingStates.CountAsync(cancellationToken),
            CosmosScheduleHistoryCount = await _cosmosContext.ScheduleHistory.CountAsync(cancellationToken),
            
            PostgresUsersCount = await _postgresContext.Users.CountAsync(cancellationToken),
            PostgresChannelStatesCount = await _postgresContext.ChannelScrapingStates.CountAsync(cancellationToken),
            PostgresScheduleHistoryCount = await _postgresContext.ScheduleHistory.CountAsync(cancellationToken)
        };

        return stats;
    }
}

/// <summary>
/// Результат міграції
/// </summary>
public class MigrationResult
{
    public DateTime StartedAt { get; set; }
    public DateTime CompletedAt { get; set; }
    public TimeSpan Duration { get; set; }
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    
    public EntityMigrationResult UsersResult { get; set; } = new();
    public EntityMigrationResult ChannelStatesResult { get; set; } = new();
    public EntityMigrationResult ScheduleHistoryResult { get; set; } = new();

    public int TotalMigrated => UsersResult.Migrated + ChannelStatesResult.Migrated + ScheduleHistoryResult.Migrated;
    public int TotalUpdated => UsersResult.Updated + ChannelStatesResult.Updated + ScheduleHistoryResult.Updated;
    public int TotalSkipped => UsersResult.Skipped + ChannelStatesResult.Skipped + ScheduleHistoryResult.Skipped;
    public int TotalFailed => UsersResult.Failed + ChannelStatesResult.Failed + ScheduleHistoryResult.Failed;
}

/// <summary>
/// Результат міграції окремої сутності
/// </summary>
public class EntityMigrationResult
{
    public string EntityType { get; set; } = string.Empty;
    public int Total { get; set; }
    public int Migrated { get; set; }
    public int Updated { get; set; }
    public int Skipped { get; set; }
    public int Failed { get; set; }
}

/// <summary>
/// Статистика по кількості записів
/// </summary>
public class MigrationStats
{
    public int CosmosUsersCount { get; set; }
    public int CosmosChannelStatesCount { get; set; }
    public int CosmosScheduleHistoryCount { get; set; }
    
    public int PostgresUsersCount { get; set; }
    public int PostgresChannelStatesCount { get; set; }
    public int PostgresScheduleHistoryCount { get; set; }
}
