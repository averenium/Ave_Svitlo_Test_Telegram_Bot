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

    // Кеш оброблених повідомлень (в пам'яті), ключ — "канал:id" (ID у різних каналах можуть збігатися)
    private static readonly ConcurrentDictionary<string, DateTime> _processedMessages = new();
    // Кеш текстів повідомлень для дедуплікації між каналами: хеш тексту -> ключ повідомлення
    private static readonly ConcurrentDictionary<string, string> _processedTexts = new();
    // Кеш графіків: зміст графіка -> пересилання цього змісту (відбиток, час публікації, ключ повідомлення).
    // Чи це той самий графік, вирішує ChannelMessageClassifier.IsSameSchedule
    private static readonly Dictionary<string, List<(ChannelMessageClassifier.ScheduleFingerprint Fingerprint, DateTime PublishedUtc, string MessageKey)>> _processedSchedules = new();
    private static readonly object _schedulesLock = new();
    private static readonly TimeSpan _cacheExpiration = TimeSpan.FromHours(24);

    // Налаштовані канали
    private const string Channel1Username = "dnipro_svet";
    private const string Channel2Username = "ddnepr_vse";

    private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);

    // Regex для видалення посилань на канал та футерів
    private static readonly Regex ChannelLinkRegex = new(@"https?://t\.me/[\w_]+", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    
    // Початок футера другого каналу (обрізаємо текст з першого знайденого маркера)
    private static readonly string[] Channel2FooterMarkers = ["Чат-боти аварійних бригад", "Єдина диспетчерська служба"];

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

            var newChannel1Messages = SelectNewMessages(channel1Messages, channel1State);
            var newChannel2Messages = SelectNewMessages(channel2Messages, channel2State);

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

            // Обробляємо повідомлення у хронологічному порядку
            foreach (var (message, channelUsername) in sortedMessages)
            {
                await ProcessMessageAsync(message, channelUsername, scrapingUsers, cancellationToken);
            }

            // Зберігаємо стан у БД для кожного каналу — за найпізніше опублікованим повідомленням,
            // а не за найбільшим ID: після перестворення каналу нумерація ID починається спочатку
            await SaveLatestStateAsync(dataService, Channel1Username, newChannel1Messages, cancellationToken);
            await SaveLatestStateAsync(dataService, Channel2Username, newChannel2Messages, cancellationToken);

            _logger.LogInformation("Скрапінг каналів завершено успішно. Оброблено {Count} нових повідомлень",
                allNewMessages.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Помилка при скрапінгу каналів");
        }
    }

    /// <summary>
    /// Нові повідомлення: ID більший за останній оброблений або опубліковані пізніше за нього.
    /// Перевірка за датою потрібна, коли канал перестворили і ID почали рахуватися з нуля
    /// (так сталося з @ddnepr_vse у грудні 2025: збережений ID ~15663 блокував усі нові пости).
    /// </summary>
    internal static List<TelegramChannelMessage> SelectNewMessages(
        List<TelegramChannelMessage> messages,
        ChannelScrapingState? state)
    {
        if (state == null)
            return messages;

        var lastProcessedAtUtc = DateTime.SpecifyKind(state.LastProcessedAt, DateTimeKind.Utc);
        return messages
            .Where(m => m.MessageId > state.LastProcessedMessageId ||
                        (m.PublishedAt != default && m.PublishedAt.ToUniversalTime() > lastProcessedAtUtc))
            .ToList();
    }

    private static async Task SaveLatestStateAsync(
        IBotDataService dataService,
        string channelUsername,
        List<TelegramChannelMessage> newMessages,
        CancellationToken cancellationToken)
    {
        var latest = newMessages
            .OrderByDescending(m => m.PublishedAt)
            .ThenByDescending(m => m.MessageId)
            .FirstOrDefault();

        if (latest == null)
            return;

        await dataService.UpdateChannelScrapingStateAsync(
            channelUsername,
            latest.MessageId,
            latest.PublishedAt,
            cancellationToken);
    }

    private async Task ProcessMessageAsync(
        TelegramChannelMessage message,
        string channelUsername,
        List<BotUser> scrapingUsers,
        CancellationToken cancellationToken)
    {
        try
        {
            var messageKey = $"{channelUsername}:{message.MessageId}";

            // Перевіряємо, чи не було повідомлення вже оброблене (локальний кеш по ID)
            if (_processedMessages.ContainsKey(messageKey))
            {
                _logger.LogDebug("Повідомлення {MessageId} з @{Channel} вже було оброблене", 
                    message.MessageId, channelUsername);
                return;
            }

            // Очищуємо текст від посилань та футерів
            var cleanText = CleanMessageText(message.Text, channelUsername);

            // Перевіряємо дублікат по тексту (між каналами)
            var textHash = GetTextHash(cleanText);
            if (_processedTexts.TryGetValue(textHash, out var existingMessageKey))
            {
                _logger.LogDebug("Повідомлення {MessageId} з @{Channel} є дублікатом повідомлення {ExistingMessageKey}",
                    message.MessageId, channelUsername, existingMessageKey);

                // Додаємо в кеш як оброблене, але не відправляємо
                _processedMessages.TryAdd(messageKey, DateTime.UtcNow);
                return;
            }

            // Парсимо повідомлення: провайдери, групи з прив'язкою до секції провайдера, тематика
            var info = ChannelMessageClassifier.Parse(cleanText);

            if (!info.IsRelevant)
            {
                _logger.LogDebug("Повідомлення {MessageId} з @{Channel} не містить інформації про відключення для провайдера або групи",
                    message.MessageId, channelUsername);

                // Додаємо в кеш як оброблене (щоб не обробляти повторно)
                _processedMessages.TryAdd(messageKey, DateTime.UtcNow);
                return;
            }

            // Той самий графік уже пересилали з іншим підписом (копія з іншого каналу або «Дублюємо…»)
            var scheduleFingerprint = ChannelMessageClassifier.GetScheduleFingerprint(cleanText);
            var publishedUtc = message.PublishedAt == default ? DateTime.UtcNow : message.PublishedAt.ToUniversalTime();
            var sameScheduleKey = FindSameSchedule(scheduleFingerprint, publishedUtc);
            if (sameScheduleKey != null)
            {
                _logger.LogInformation("Повідомлення {MessageId} з @{Channel} містить той самий графік, що й {ExistingMessageKey}",
                    message.MessageId, channelUsername, sameScheduleKey);
                _processedMessages.TryAdd(messageKey, DateTime.UtcNow);
                _processedTexts.TryAdd(textHash, messageKey);
                return;
            }

            _logger.LogInformation(
                "Повідомлення {MessageId} з @{Channel} містить інформацію (DSO: {DsoIds}, Групи: {Groups}, Загальнонаціональне: {Nationwide})",
                message.MessageId, channelUsername, string.Join(", ", info.DsoIds),
                string.Join(", ", info.Groups.Select(g => $"{g.Group}/{g.DsoId?.ToString() ?? "?"}")),
                info.IsNationwideNotice);

            // Фільтруємо користувачів, яким підходить повідомлення
            var targetUsers = scrapingUsers
                .Where(u => ChannelMessageClassifier.MatchesUser(info, u.DsoId, u.GroupName))
                .ToList();

            if (targetUsers.Count == 0)
            {
                _logger.LogInformation("Немає користувачів, яким відповідає повідомлення {MessageId} з @{Channel}",
                    message.MessageId, channelUsername);
                _processedMessages.TryAdd(messageKey, DateTime.UtcNow);
                _processedTexts.TryAdd(textHash, messageKey);
                RememberSchedule(scheduleFingerprint, publishedUtc, messageKey);
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
            _processedMessages.TryAdd(messageKey, DateTime.UtcNow);
            _processedTexts.TryAdd(textHash, messageKey);
            RememberSchedule(scheduleFingerprint, publishedUtc, messageKey);

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

        // Видаляємо футер з другого каналу: «Чат-боти аварійних бригад 👉Світло | водопостачання👈
        // Єдина диспетчерська служба…». Слово «аварійних» у футері інакше вважалося б аварійним алертом
        if (channelUsername == Channel2Username)
        {
            var footerIndex = Channel2FooterMarkers
                .Select(marker => cleanText.IndexOf(marker, StringComparison.OrdinalIgnoreCase))
                .Where(index => index >= 0)
                .DefaultIfEmpty(-1)
                .Min();
            if (footerIndex >= 0)
            {
                cleanText = cleanText.Substring(0, footerIndex);
            }
        }

        // Обрізаємо зайві пробіли та переноси рядків
        return cleanText.Trim();
    }

    private static string GetTextHash(string text)
    {
        // Нормалізуємо текст для порівняння (видаляємо зайві пробіли, переноси, приводимо до нижнього регістра).
        // Хешуємо весь текст: оновлення графіка з тим самим початком (зміни лише у 5.1 внизу) не є дублікатом
        var normalized = WhitespaceRegex.Replace(text.ToLowerInvariant(), " ").Trim();
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(normalized)));
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

        // Графіки тримаємо добу від публікації
        var expiredScheduleCount = 0;
        lock (_schedulesLock)
        {
            foreach (var content in _processedSchedules.Keys.ToList())
            {
                var entries = _processedSchedules[content];
                expiredScheduleCount += entries.RemoveAll(e => now - e.PublishedUtc > ChannelMessageClassifier.SameDateScheduleWindow);
                if (entries.Count == 0)
                    _processedSchedules.Remove(content);
            }
        }

        if (expiredMessageKeys.Count > 0 || expiredTextKeys.Count > 0 || expiredScheduleCount > 0)
        {
            _logger.LogDebug("Очищено {MessageCount} повідомлень, {TextCount} текстів та {ScheduleCount} графіків з кешу",
                expiredMessageKeys.Count, expiredTextKeys.Count, expiredScheduleCount);
        }
    }

    /// <summary>
    /// Ключ повідомлення, яке вже переслало цей самий графік, або null
    /// </summary>
    private static string? FindSameSchedule(ChannelMessageClassifier.ScheduleFingerprint? fingerprint, DateTime publishedUtc)
    {
        if (fingerprint == null)
            return null;

        lock (_schedulesLock)
        {
            if (!_processedSchedules.TryGetValue(fingerprint.Content, out var entries))
                return null;

            return entries
                .FirstOrDefault(e => ChannelMessageClassifier.IsSameSchedule(fingerprint, publishedUtc, e.Fingerprint, e.PublishedUtc))
                .MessageKey;
        }
    }

    private static void RememberSchedule(ChannelMessageClassifier.ScheduleFingerprint? fingerprint, DateTime publishedUtc, string messageKey)
    {
        if (fingerprint == null)
            return;

        lock (_schedulesLock)
        {
            if (!_processedSchedules.TryGetValue(fingerprint.Content, out var entries))
                _processedSchedules[fingerprint.Content] = entries = new();
            entries.Add((fingerprint, publishedUtc, messageKey));
        }
    }
}
