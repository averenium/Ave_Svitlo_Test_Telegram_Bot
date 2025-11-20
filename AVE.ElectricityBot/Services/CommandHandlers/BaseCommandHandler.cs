using AVE.ElectricityBot.Configuration;
using AVE.ElectricityBot.Data.Abstractions;
using AVE.ElectricityBot.Services.Interfaces;
using AVE.ElectricityBot.YasnoApi;
using AVE.ElectricityBot.YasnoApi.Models;
using Microsoft.Extensions.Options;
using Telegram.Bot.Types;

namespace AVE.ElectricityBot.Services.CommandHandlers;

/// <summary>
/// Базовий клас для обробників команд
/// </summary>
public abstract class BaseCommandHandler : ICommandHandler
{
    protected readonly IYasnoApiClient YasnoApiClient;
    protected readonly YasnoAddressConfiguration AddressConfig;
    protected readonly IMessageFormatterService MessageFormatter;
    protected readonly ILogger Logger;
    protected readonly IBotDataService DataService;
    protected readonly IScheduleCacheService CacheService;

    protected BaseCommandHandler(
        IYasnoApiClient yasnoApiClient,
        IOptions<YasnoAddressConfiguration> addressConfig,
        IMessageFormatterService messageFormatter,
        ILogger logger,
        IBotDataService dataService,
        IScheduleCacheService cacheService)
    {
        YasnoApiClient = yasnoApiClient;
        AddressConfig = addressConfig.Value;
        MessageFormatter = messageFormatter;
        Logger = logger;
        DataService = dataService;
        CacheService = cacheService;
    }

    public abstract string CommandName { get; }
    public abstract string Description { get; }
    public abstract Task<string> HandleAsync(Message message, CancellationToken cancellationToken);

    /// <summary>
    /// Отримати групу та DSO користувача з БД
    /// </summary>
    protected async Task<(int group, int subgroup, int dsoId)?> GetUserGroupAndDsoAsync(long userId, CancellationToken cancellationToken)
    {
        var users = await DataService.GetActiveUsersAsync(cancellationToken);
        var user = users.FirstOrDefault(u => u.TelegramUserId == userId);

        if (user == null || string.IsNullOrEmpty(user.GroupName) || !user.DsoId.HasValue)
        {
            return null;
        }

        var parts = user.GroupName.Split('.');
        if (parts.Length == 2 && 
            int.TryParse(parts[0], out var group) && 
            int.TryParse(parts[1], out var subgroup))
        {
            return (group, subgroup, user.DsoId.Value);
        }

        return null;
    }

    /// <summary>
    /// Отримати групу користувача з БД (старий метод для сумісності)
    /// </summary>
    protected async Task<(int group, int subgroup)?> GetUserGroupAsync(long userId, CancellationToken cancellationToken)
    {
        var result = await GetUserGroupAndDsoAsync(userId, cancellationToken);
        if (result.HasValue)
        {
            return (result.Value.group, result.Value.subgroup);
        }
        return null;
    }

    /// <summary>
    /// Отримати розклад для групи користувача (з кешу)
    /// </summary>
    protected async Task<GroupOutageSchedule?> GetUserScheduleAsync(long userId, CancellationToken cancellationToken)
    {
        var userGroupAndDso = await GetUserGroupAndDsoAsync(userId, cancellationToken);
        if (!userGroupAndDso.HasValue)
        {
            return null;
        }

        var (group, subgroup, dsoId) = userGroupAndDso.Value;

        // Використовуємо кеш з правильним DSO
        var outages = await CacheService.GetPlannedOutagesAsync(dsoId, cancellationToken);

        return outages?.GetSchedule(group, subgroup);
    }
}
