namespace AVE.ElectricityBot.Data.Interfaces;

using AVE.ElectricityBot.Data.Entities;

/// <summary>
/// Сервіс для роботи з базою даних
/// </summary>
public interface IBotDataService
{
    /// <summary>
    /// Зберегти розклад (викликається раз на годину)
    /// </summary>
    Task<ScheduleHistory> SaveScheduleHistoryAsync(
        DateTime scheduleDate,
        int totalOutageMinutes,
        int outagePeriods,
        string status,
        string slotsJson,
        string groupName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Отримати історію розкладу за період
    /// </summary>
    Task<List<ScheduleHistory>> GetScheduleHistoryAsync(
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Отримати останній збережений розклад для дати
    /// </summary>
    Task<ScheduleHistory?> GetLatestScheduleForDateAsync(
        DateTime date,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Перевірити чи потрібно зберігати новий запис (раз на годину)
    /// </summary>
    Task<bool> ShouldSaveNewRecordAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Отримати або створити користувача
    /// </summary>
    Task<BotUser> GetOrCreateUserAsync(
        long telegramUserId,
        long telegramChatId,
        string? username,
        string? firstName,
        string? lastName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Оновити активність користувача
    /// </summary>
    Task UpdateUserActivityAsync(
        long telegramUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Оновити групу користувача
    /// </summary>
    Task UpdateUserGroupAsync(
        long telegramUserId,
        string groupName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Оновити групу та DSO користувача
    /// </summary>
    Task UpdateUserGroupAndDsoAsync(
        long telegramUserId,
        string groupName,
        int dsoId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Отримати всіх активних користувачів
    /// </summary>
    Task<List<BotUser>> GetActiveUsersAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Оновити налаштування збору даних з каналу користувача
    /// </summary>
    Task UpdateUserChannelScrapingAsync(
        long telegramUserId,
        bool enableChannelScraping,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Отримати користувачів з увімкненим збором даних з каналу
    /// </summary>
    Task<List<BotUser>> GetUsersWithChannelScrapingEnabledAsync(
        CancellationToken cancellationToken = default);

    // === Методи для роботи зі станом скрапінгу каналів ===

    /// <summary>
    /// Отримати стан скрапінгу для каналу
    /// </summary>
    Task<ChannelScrapingState?> GetChannelScrapingStateAsync(
        string channelUsername,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Оновити стан скрапінгу каналу (ID останнього обробленого повідомлення)
    /// </summary>
    Task UpdateChannelScrapingStateAsync(
        string channelUsername,
        long lastProcessedMessageId,
        DateTime lastProcessedAt,
        CancellationToken cancellationToken = default);
}
