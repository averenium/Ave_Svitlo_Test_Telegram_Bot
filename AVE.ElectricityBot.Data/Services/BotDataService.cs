using AVE.ElectricityBot.Data.Abstractions;
using AVE.ElectricityBot.Data.Context;
using AVE.ElectricityBot.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AVE.ElectricityBot.Data.Services;

/// <summary>
/// Сервіс для роботи з ClickHouse
/// </summary>
public class BotDataService : IBotDataService
{
    private readonly PostgresDbContext _context;
    private readonly ILogger<BotDataService> _logger;

    public BotDataService(PostgresDbContext context, ILogger<BotDataService> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ScheduleHistory> SaveScheduleHistoryAsync(
        DateTime scheduleDate,
        int totalOutageMinutes,
        int outagePeriods,
        string status,
        string slotsJson,
        string groupName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var now = DateTime.UtcNow;
            var recordedAtHour = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0, DateTimeKind.Utc);

            var history = new ScheduleHistory
            {
                RecordedAt = recordedAtHour,
                ScheduleDate = scheduleDate.Date.ToUniversalTime(),
                TotalOutageMinutes = totalOutageMinutes,
                OutagePeriods = outagePeriods,
                Status = status,
                SlotsJson = slotsJson,
                GroupName = groupName,
                PartitionKey = scheduleDate.ToString("yyyy-MM") // Залишено для сумісності
            };

            _context.ScheduleHistory.Add(history);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("📊 Історія збережена: {Date} - {TotalMinutes} хв",
                scheduleDate.Date, totalOutageMinutes);

            return history;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Помилка при збереженні історії: {Date}", scheduleDate.Date);
            throw;
        }
    }

    public async Task<List<ScheduleHistory>> GetScheduleHistoryAsync(
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var history = await _context.ScheduleHistory
                .Where(h => h.ScheduleDate >= from.Date && h.ScheduleDate <= to.Date)
                .OrderBy(h => h.ScheduleDate)
                .ThenBy(h => h.RecordedAt)
                .ToListAsync(cancellationToken);

            return history;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Помилка при отриманні історії");
            throw;
        }
    }

    public async Task<ScheduleHistory?> GetLatestScheduleForDateAsync(
        DateTime date,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var history = await _context.ScheduleHistory
                .Where(h => h.ScheduleDate == date.Date.ToUniversalTime())
                .OrderByDescending(h => h.RecordedAt)
                .FirstOrDefaultAsync(cancellationToken);

            return history;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Помилка при отриманні останньої історії: {Date}", date.Date);
            throw;
        }
    }

    public async Task<bool> ShouldSaveNewRecordAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var now = DateTime.UtcNow;
            var currentHour = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0, DateTimeKind.Utc);

            // Для ClickHouse: перевіряємо чи є запис за поточну годину
            var existingRecord = await _context.ScheduleHistory
                .AnyAsync(h => h.RecordedAt == currentHour, cancellationToken);

            return !existingRecord; // true якщо запису ще немає
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Помилка при перевірці необхідності збереження");
            return false;
        }
    }

    public async Task<BotUser> GetOrCreateUserAsync(
        long telegramUserId,
        long telegramChatId,
        string? username,
        string? firstName,
        string? lastName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Шукаємо користувача
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.TelegramUserId == telegramUserId, cancellationToken);

            if (user == null)
            {
                // Створюємо нового
                user = new BotUser
                {
                    TelegramUserId = telegramUserId,
                    TelegramChatId = telegramChatId,
                    Username = username,
                    FirstName = firstName,
                    LastName = lastName,
                    FirstSeenAt = DateTime.UtcNow,
                    LastActiveAt = DateTime.UtcNow,
                    CommandsCount = 1,
                    Settings = new UserSettings(), // Явно ініціалізуємо Settings
                    PartitionKey = "users" // Залишено для сумісності
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("👤 Новий користувач: {Username} (ID: {UserId})",
                    username ?? "Unknown", telegramUserId);
            }
            else
            {
                // Перевіряємо чи Settings не null (для існуючих користувачів з БД)
                if (user.Settings == null)
                {
                    user.Settings = new UserSettings();
                }

                // Оновлюємо існуючого
                user.LastActiveAt = DateTime.UtcNow;
                user.CommandsCount++;
                
                // Оновлюємо дані якщо змінились
                if (user.Username != username || user.FirstName != firstName || user.LastName != lastName)
                {
                    user.Username = username;
                    user.FirstName = firstName;
                    user.LastName = lastName;
                }

                // Не викликаємо Update, EF Core сам відстежує зміни
                await _context.SaveChangesAsync(cancellationToken);
            }

            return user;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Помилка при роботі з користувачем: {UserId}", telegramUserId);
            throw;
        }
    }

    public async Task UpdateUserActivityAsync(
        long telegramUserId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.TelegramUserId == telegramUserId, cancellationToken);

            if (user != null)
            {
                // Перевіряємо чи Settings не null
                if (user.Settings == null)
                {
                    user.Settings = new UserSettings();
                }

                user.LastActiveAt = DateTime.UtcNow;
                user.CommandsCount++;

                // Не викликаємо Update, EF Core сам відстежує зміни
                await _context.SaveChangesAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Помилка при оновленні активності: {UserId}", telegramUserId);
        }
    }

    public async Task UpdateUserGroupAsync(
        long telegramUserId,
        string groupName,
        CancellationToken cancellationToken = default)
    {
        var user = await GetOrCreateUserAsync(telegramUserId, telegramUserId, null, null, null, cancellationToken);
        
        user.GroupName = groupName;
        user.LastActiveAt = DateTime.UtcNow;

        // Не викликаємо Update, EF Core сам відстежує зміни
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Оновлено групу користувача {UserId}: {GroupName}", telegramUserId, groupName);
    }

    public async Task UpdateUserGroupAndDsoAsync(
        long telegramUserId,
        string groupName,
        int dsoId,
        CancellationToken cancellationToken = default)
    {
        var user = await GetOrCreateUserAsync(telegramUserId, telegramUserId, null, null, null, cancellationToken);

        user.GroupName = groupName;
        user.DsoId = dsoId;
        user.LastActiveAt = DateTime.UtcNow;

        // Не викликаємо Update, EF Core сам відстежує зміни
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Оновлено групу та DSO користувача {UserId}: {GroupName}, DSO={DsoId}",
            telegramUserId, groupName, dsoId);
    }

    public async Task<List<BotUser>> GetActiveUsersAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var users = await _context.Users
                .Where(u => u.IsActive)
                .ToListAsync(cancellationToken);

            return users;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Помилка при отриманні активних користувачів");
            throw;
        }
    }

    public async Task UpdateUserChannelScrapingAsync(
        long telegramUserId,
        bool enableChannelScraping,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.TelegramUserId == telegramUserId, cancellationToken);

            if (user != null)
            {
                // Перевіряємо чи Settings не null
                if (user.Settings == null)
                {
                    user.Settings = new UserSettings();
                }

                user.Settings.EnableChannelScraping = enableChannelScraping;
                user.LastActiveAt = DateTime.UtcNow;

                // Не викликаємо Update, EF Core сам відстежує зміни
                await _context.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("Оновлено налаштування парсингу каналів для користувача {UserId}: {Enabled}",
                    telegramUserId, enableChannelScraping);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Помилка при оновленні налаштування парсингу каналів: {UserId}", telegramUserId);
            throw;
        }
    }

    public async Task<List<BotUser>> GetUsersWithChannelScrapingEnabledAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var users = await _context.Users
                .Where(u => u.IsActive
                    && u.Settings != null
                    && u.Settings.EnableChannelScraping
                    && !string.IsNullOrEmpty(u.GroupName)
                    && u.DsoId.HasValue)
                .ToListAsync(cancellationToken);

            return users;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Помилка при отриманні користувачів з увімкненим скрапінгом каналів");
            throw;
        }
    }

    // === Методи для роботи зі станом скрапінгу каналів ===

    public async Task<ChannelScrapingState?> GetChannelScrapingStateAsync(
        string channelUsername,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var state = await _context.ChannelScrapingStates
                .FirstOrDefaultAsync(s => s.ChannelUsername == channelUsername, cancellationToken);

            return state;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Помилка при отриманні стану скрапінгу для каналу @{Channel}", channelUsername);
            throw;
        }
    }

    public async Task UpdateChannelScrapingStateAsync(
        string channelUsername,
        long lastProcessedMessageId,
        DateTime lastProcessedAt,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Отримуємо існуючий стан
            var state = await GetChannelScrapingStateAsync(channelUsername, cancellationToken);

            if (state == null)
            {
                // Створюємо новий стан
                state = new ChannelScrapingState
                {
                    Id = $"channel:{channelUsername}",
                    ChannelUsername = channelUsername,
                    LastProcessedMessageId = lastProcessedMessageId,
                    LastProcessedAt = lastProcessedAt.ToUniversalTime(),
                    LastScrapeAt = DateTime.UtcNow,
                    TotalProcessedMessages = 1,
                    PartitionKey = "channel_state" // Залишено для сумісності
                };

                _context.ChannelScrapingStates.Add(state);
            }
            else
            {
                // Оновлюємо існуючий стан
                state.LastProcessedMessageId = lastProcessedMessageId;
                state.LastProcessedAt = lastProcessedAt.ToUniversalTime();
                state.LastScrapeAt = DateTime.UtcNow;
                state.TotalProcessedMessages++;

                // НЕ викликаємо Update, EF Core сам відстежить зміни
            }

            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Оновлено стан скрапінгу для @{Channel}: останнє повідомлення {MessageId}",
                channelUsername, lastProcessedMessageId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Помилка при оновленні стану скрапінгу для @{Channel}", channelUsername);
            throw;
        }
    }
}
