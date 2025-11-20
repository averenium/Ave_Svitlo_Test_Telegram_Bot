using AVE.ElectricityBot.Configuration;
using AVE.ElectricityBot.Data.Abstractions;
using AVE.ElectricityBot.Services.Interfaces;
using AVE.ElectricityBot.YasnoApi;
using Microsoft.Extensions.Options;
using Telegram.Bot.Types;

namespace AVE.ElectricityBot.Services.CommandHandlers;

/// <summary>
/// Обробник команди /stats - статистика користувачів (для адміна)
/// </summary>
public class StatsCommandHandler : BaseCommandHandler
{
    private readonly IBotDataService _dataService;

    public StatsCommandHandler(
        IYasnoApiClient yasnoApiClient,
        IOptions<YasnoAddressConfiguration> addressConfig,
        IMessageFormatterService messageFormatter,
        ILogger<StatsCommandHandler> logger,
        IBotDataService dataService,
        IScheduleCacheService cacheService) 
        : base(yasnoApiClient, addressConfig, messageFormatter, logger, dataService, cacheService)
    {
        _dataService = dataService;
    }

    public override string CommandName => "stats";
    public override string Description => "?? Статистика користувачів";

    public override async Task<string> HandleAsync(Message message, CancellationToken cancellationToken)
    {
        var users = await _dataService.GetActiveUsersAsync(cancellationToken);

        if (!users.Any())
        {
            return "?? Користувачів поки немає";
        }

        var totalCommands = users.Sum(u => u.CommandsCount);
        var usersWithGroups = users.Count(u => !string.IsNullOrEmpty(u.GroupName));

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("?? <b>СТАТИСТИКА КОРИСТУВАЧІВ</b>");
        sb.AppendLine();
        sb.AppendLine($"?? Всього користувачів: <b>{users.Count}</b>");
        sb.AppendLine($"?? З налаштованою групою: <b>{usersWithGroups}</b>");
        sb.AppendLine($"?? Всього команд: <b>{totalCommands}</b>");
        sb.AppendLine();
        sb.AppendLine("????????????????????");
        sb.AppendLine();
        sb.AppendLine("<b>Топ користувачів:</b>");
        sb.AppendLine();

        var topUsers = users.OrderByDescending(u => u.CommandsCount).Take(5);
        var position = 1;

        foreach (var user in topUsers)
        {
            var displayName = user.FirstName ?? user.Username ?? $"User{user.TelegramUserId}";
            var group = !string.IsNullOrEmpty(user.GroupName) ? $" (група {user.GroupName})" : "";
            
            sb.AppendLine($"{position}. {displayName}{group}");
            sb.AppendLine($"   ?? Команд: {user.CommandsCount}");
            sb.AppendLine($"   ?? Остання активність: {user.LastActiveAt.ToLocalTime():dd.MM HH:mm}");
            sb.AppendLine();
            position++;
        }

        return sb.ToString();
    }
}
