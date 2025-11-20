using AVE.ElectricityBot.Data;
using AVE.ElectricityBot.Data.Entities;
using AVE.ElectricityBot.Data.Abstractions;
using AVE.ElectricityBot.Services.Interfaces;
using AVE.ElectricityBot.TelegramScraper;
using AVE.ElectricityBot.TelegramScraper.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace AVE.ElectricityBot.Services.Implementations;

/// <summary>
/// Сервіс для скрапінгу Telegram каналів та пересилки повідомлень користувачам
/// </summary>
public class ChannelScraperService : IChannelScraperService
{
    private readonly ITelegramChannelScraper _scraper;
    private readonly ITelegramBotClient _botClient;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ChannelScraperService> _logger;

    // Кеш оброблених повідомлень (в пам'яті)
    private static readonly ConcurrentDictionary<long, DateTime> _processedMessages = new();
    // Кеш текстів повідомлень для дедуплікації між каналами
    private static readonly ConcurrentDictionary<string, long> _processedTexts = new();
    private static readonly TimeSpan _cacheExpiration = TimeSpan.FromHours(24);

    // Налаштовані канали
    private const string Channel1Username = "dnipro_svet";
    private const string Channel2Username = "ddnepr_vse";

    // Regex для пошуку провайдера та груп
    private static readonly Regex DsoRegex = new(@"ДСО\s*(?:Д|Ц|\d+|ДТЕК)", RegexOptions.IgnoreCase);
    // Regex для пошук груп (простий формат: 1.1, 2.2, 5.1 тощо)
    private static readonly Regex GroupRegex = new(@"\b(\d+\.\d+)\b", RegexOptions.Compiled);
    
    // Regex для видалення посилань на канал та футерів
    private static readonly Regex ChannelLinkRegex = new(@"https?://t\.me/[\w_]+", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    
    // Regex для видалення футера з другого каналу (більш гнучкий)
    private static readonly Regex Channel2FooterRegex = new(
        @"Єдина диспетчерська служба.*?(?:водопостачання|$)", 
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    public ChannelScraperService(
        ITelegramChannelScraper scraper,
        ITelegramBotClient botClient,
        IServiceScopeFactory scopeFactory,
        ILogger<ChannelScraperService> logger)
    {
        _scraper = scraper;
        _botClient = botClient;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task ScrapeAndForwardMessagesAsync(CancellationToken cancellationToken = default)
    {
        // Створюємо новий scope для ізоляції DbContext
        using var scope = _scopeFactory.CreateScope();
        var dataService = scope.ServiceProvider.GetRequiredService<IBotDataService>();

        try
        {
            _logger.LogInformation("Початок скрапінгу каналів");

            // Очищуємо старі записи з кешу
            CleanupExpiredCache();

            // Отримуємо стан скрапінгу для обох каналів з БД
            var channel1State = await dataService.GetChannelScrapingStateAsync(Channel1Username, cancellationToken);
            var channel2State = await dataService.GetChannelScrapingStateAsync(Channel2Username, cancellationToken);

            // Отримуємо повідомлення з обох каналів паралельно
            var channel1Task = _scraper.GetLatestMessagesAsync(Channel1Username, 10, cancellationToken);
            var channel2Task = _scraper.GetLatestMessagesAsync(Channel2Username, 10, cancellationToken);

            await Task.WhenAll(channel1Task, channel2Task);

            var channel1Messages = await channel1Task;
            var channel2Messages = await channel2Task;

            // Фільтруємо повідомлення - беремо тільки нові (після останнього обробленого)
            var channel1LastId = channel1State?.LastProcessedMessageId ?? 0;
            var channel2LastId = channel2State?.LastProcessedMessageId ?? 0;

            var newChannel1Messages = channel1Messages.Where(m => m.MessageId > channel1LastId).ToList();
            var newChannel2Messages = channel2Messages.Where(m => m.MessageId > channel2LastId).ToList();

            _logger.LogInformation(
                "Отримано {Count1} повідомлень з @{Channel1} (нових: {New1}, останнє оброблене: {Last1}) " +
                "та {Count2} повідомлень з @{Channel2} (нових: {New2}, останнє оброблене: {Last2})",
                channel1Messages.Count, Channel1Username, newChannel1Messages.Count, channel1LastId,
                channel2Messages.Count, Channel2Username, newChannel2Messages.Count, channel2LastId);

            // Об'єднуємо тільки НОВІ повідомлення з обох каналів
            var allNewMessages = new List<(TelegramChannelMessage message, string channelUsername)>();
            allNewMessages.AddRange(newChannel1Messages.Select(m => (m, Channel1Username)));
            allNewMessages.AddRange(newChannel2Messages.Select(m => (m, Channel2Username)));

            if (allNewMessages.Count == 0)
            {
                _logger.LogInformation("Не знайдено нових повідомлень у каналах");
                return;
            }

            // Отримуємо всіх користувачів з увімкненим скрапінгом каналів
            var scrapingUsers = await dataService.GetUsersWithChannelScrapingEnabledAsync(cancellationToken);

            if (scrapingUsers.Count == 0)
            {
                _logger.LogInformation("Немає користувачів з увімкненим скрапінгом каналів");
                return;
            }

            _logger.LogInformation("Знайдено {Count} користувачів з увімкненим скрапінгом каналів", scrapingUsers.Count);

            // Сортуємо повідомлення за датою (від найстаріших до найновіших)
            var sortedMessages = allNewMessages
                .OrderBy(m => m.message.PublishedAt)
                .ToList();

            // Відстежуємо найновіші повідомлення для кожного каналу
            long latestChannel1MessageId = channel1LastId;
            long latestChannel2MessageId = channel2LastId;

            // Обробляємо повідомлення у хронологічному порядку
            foreach (var (message, channelUsername) in sortedMessages)
            {
                await ProcessMessageAsync(message, channelUsername, scrapingUsers, cancellationToken);

                // Оновлюємо ID найновішого обробленого повідомлення
                if (channelUsername == Channel1Username && message.MessageId > latestChannel1MessageId)
                {
                    latestChannel1MessageId = message.MessageId;
                }
                else if (channelUsername == Channel2Username && message.MessageId > latestChannel2MessageId)
                {
                    latestChannel2MessageId = message.MessageId;
                }
            }

            // Зберігаємо стан у БД для кожного каналу
            if (latestChannel1MessageId > channel1LastId)
            {
                var latestChannel1Message = newChannel1Messages
                    .OrderByDescending(m => m.MessageId)
                    .First();
                
                await dataService.UpdateChannelScrapingStateAsync(
                    Channel1Username,
                    latestChannel1MessageId,
                    latestChannel1Message.PublishedAt,
                    cancellationToken);
            }

            if (latestChannel2MessageId > channel2LastId)
            {
                var latestChannel2Message = newChannel2Messages
                    .OrderByDescending(m => m.MessageId)
                    .First();
                
                await dataService.UpdateChannelScrapingStateAsync(
                    Channel2Username,
                    latestChannel2MessageId,
                    latestChannel2Message.PublishedAt,
                    cancellationToken);
            }

            _logger.LogInformation("Скрапінг каналів завершено успішно. Оброблено {Count} нових повідомлень",
                allNewMessages.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Помилка при скрапінгу каналів");
        }
    }

    private async Task ProcessMessageAsync(
        TelegramChannelMessage message,
        string channelUsername,
        List<BotUser> scrapingUsers,
        CancellationToken cancellationToken)
    {
        try
        {
            // Перевіряємо, чи не було повідомлення вже оброблене (локальний кеш по ID)
            if (_processedMessages.ContainsKey(message.MessageId))
            {
                _logger.LogDebug("Повідомлення {MessageId} з @{Channel} вже було оброблене", 
                    message.MessageId, channelUsername);
                return;
            }

            // Очищуємо текст від посилань та футерів
            var cleanText = CleanMessageText(message.Text, channelUsername);

            // Перевіряємо дублікат по тексту (між каналами)
            var textHash = GetTextHash(cleanText);
            if (_processedTexts.TryGetValue(textHash, out var existingMessageId))
            {
                _logger.LogDebug("Повідомлення {MessageId} з @{Channel} є дублікатом повідомлення {ExistingMessageId}", 
                    message.MessageId, channelUsername, existingMessageId);
                
                // Додаємо в кеш як оброблене, але не відправляємо
                _processedMessages.TryAdd(message.MessageId, DateTime.UtcNow);
                return;
            }

            // Парсимо повідомлення на пошук DSO і групу
            var (dsoId, groups) = ParseMessageInfo(cleanText);

            if (!dsoId.HasValue && groups.Count == 0)
            {
                _logger.LogDebug("Повідомлення {MessageId} з @{Channel} не містить інформації про провайдера або групу відключень", 
                    message.MessageId, channelUsername);
                
                // Додаємо в кеш як оброблене (щоб не обробляти повторно)
                _processedMessages.TryAdd(message.MessageId, DateTime.UtcNow);
                return;
            }

            _logger.LogInformation("Повідомлення {MessageId} з @{Channel} містить інформацію (DSO: {DsoId}, Групи: {Groups})",
                message.MessageId, channelUsername, dsoId, string.Join(", ", groups));

            // Фільтруємо користувачів, яким підходить повідомлення
            var targetUsers = scrapingUsers
                .Where(u => MatchesUser(u, dsoId, groups))
                .ToList();

            if (targetUsers.Count == 0)
            {
                _logger.LogInformation("Немає користувачів, яким відповідає повідомлення {MessageId} з @{Channel}", 
                    message.MessageId, channelUsername);
                _processedMessages.TryAdd(message.MessageId, DateTime.UtcNow);
                _processedTexts.TryAdd(textHash, message.MessageId);
                return;
            }

            _logger.LogInformation("Відправлення повідомлення {MessageId} з @{Channel} до {Count} користувачів", 
                message.MessageId, channelUsername, targetUsers.Count);

            // Пересилаємо повідомлення користувачам
            int forwardedCount = 0;
            foreach (var user in targetUsers)
            {
                try
                {
                    await _botClient.SendMessage(
                        chatId: user.TelegramChatId,
                        text: FormatForwardedMessage(message, channelUsername, cleanText),
                        parseMode: Telegram.Bot.Types.Enums.ParseMode.Html,
                        linkPreviewOptions: new Telegram.Bot.Types.LinkPreviewOptions { IsDisabled = true },
                        cancellationToken: cancellationToken);

                    forwardedCount++;
                    _logger.LogDebug("Повідомлення {MessageId} успішно переслано користувачу {UserId}",
                        message.MessageId, user.TelegramUserId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Помилка при пересилці повідомлення {MessageId} користувачу {UserId}",
                        message.MessageId, user.TelegramUserId);
                }
            }

            // Додаємо в кеш оброблених повідомлень
            _processedMessages.TryAdd(message.MessageId, DateTime.UtcNow);
            _processedTexts.TryAdd(textHash, message.MessageId);

            _logger.LogInformation("Повідомлення {MessageId} з @{Channel} переслано {ForwardedCount}/{TotalCount} користувачів",
                message.MessageId, channelUsername, forwardedCount, targetUsers.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Помилка при обробці повідомлення {MessageId} з @{Channel}", 
                message.MessageId, channelUsername);
        }
    }

    private string CleanMessageText(string text, string channelUsername)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var cleanText = text;

        // Видаляємо посилання на канали
        cleanText = ChannelLinkRegex.Replace(cleanText, "");

        // Видаляємо футер з другого каналу (все після "Єдина диспетчерська служба")
        if (channelUsername == Channel2Username)
        {
            var footerIndex = cleanText.IndexOf("Єдина диспетчерська служба", StringComparison.OrdinalIgnoreCase);
            if (footerIndex >= 0)
            {
                cleanText = cleanText.Substring(0, footerIndex);
            }
        }

        // Обрізаємо зайві пробіли та переноси рядків
        return cleanText.Trim();
    }

    private string GetTextHash(string text)
    {
        // Нормалізуємо текст для порівняння (видаляємо зайві пробіли, переноси, приводимо до нижнього регістра)
        var normalized = Regex.Replace(text.ToLowerInvariant(), @"\s+", " ").Trim();
        
        // Використовуємо простий хеш для порівняння
        return normalized.Length > 100 ? normalized.Substring(0, 100) : normalized;
    }

    private (int? dsoId, List<string> groups) ParseMessageInfo(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return (null, new List<string>());

        int? dsoId = null;
        var groups = new List<string>();

        // Переводимо текст у нижній регістр для пошуку
        var lowerText = text.ToLowerInvariant();

        // Шукаємо провайдера (просто по ключовим словам)
        if (lowerText.Contains("дтек"))
        {
            dsoId = 301;
        }
        else if (lowerText.Contains("цек"))
        {
            dsoId = 303;
        }

        // Шукаємо всі групи формату X.Y
        var matches = GroupRegex.Matches(text);
        foreach (Match match in matches)
        {
            var group = match.Groups[1].Value;
            if (!groups.Contains(group))
            {
                groups.Add(group);
            }
        }

        return (dsoId, groups);
    }

    private bool MatchesUser(BotUser user, int? dsoId, List<string> groups)
    {
        // Якщо вказано DSO - перевіряємо відповідність
        if (dsoId.HasValue && user.DsoId.HasValue && user.DsoId.Value != dsoId.Value)
        {
            return false;
        }

        // Якщо є групи - перевіряємо чи користувацька група в списку
        if (groups.Count > 0 && !string.IsNullOrEmpty(user.GroupName))
        {
            return groups.Contains(user.GroupName, StringComparer.OrdinalIgnoreCase);
        }

        // Якщо вказано хоч би DSO або є групи - приймаємо
        return dsoId.HasValue || groups.Count > 0;
    }

    private string FormatForwardedMessage(
        TelegramChannelMessage message,
        string channelUsername,
        string cleanText)
    {
        // Форматуємо дату і час без року (dd.MM HH:mm)
        var publishedTime = message.PublishedAt.ToLocalTime().ToString("dd.MM HH:mm");
        
        return $"📢 <b>Повідомлення з каналу @{channelUsername}</b> ({publishedTime})\n\n" +
               $"{cleanText}\n\n" +
               $"<a href=\"{message.Url}\">📱 Переглянути в каналі</a>";
    }

    private void CleanupExpiredCache()
    {
        var now = DateTime.UtcNow;
        
        // Очищуємо кеш оброблених повідомлень
        var expiredMessageKeys = _processedMessages
            .Where(kvp => now - kvp.Value > _cacheExpiration)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in expiredMessageKeys)
        {
            _processedMessages.TryRemove(key, out _);
        }

        // Очищуємо кеш текстів (видаляємо записи старші за 24 години)
        var expiredTextKeys = _processedTexts
            .Where(kvp => !_processedMessages.ContainsKey(kvp.Value))
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in expiredTextKeys)
        {
            _processedTexts.TryRemove(key, out _);
        }

        if (expiredMessageKeys.Count > 0 || expiredTextKeys.Count > 0)
        {
            _logger.LogDebug("Очищено {MessageCount} повідомлень та {TextCount} текстів з кешу", 
                expiredMessageKeys.Count, expiredTextKeys.Count);
        }
    }
}
