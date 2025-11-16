using AVE.ElectricityBot.TelegramScraper.Models;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Text.RegularExpressions;

namespace AVE.ElectricityBot.TelegramScraper;

/// <summary>
/// Сервіс для scraping публічних Telegram каналів через t.me/s/
/// </summary>
public class TelegramChannelScraper : ITelegramChannelScraper
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TelegramChannelScraper> _logger;
    private const string TelegramBaseUrl = "https://t.me/s/";
    private const string UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

    public TelegramChannelScraper(HttpClient httpClient, ILogger<TelegramChannelScraper> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<TelegramChannelInfo?> GetChannelInfoAsync(
        string channelUsername, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"{TelegramBaseUrl}{channelUsername}";
            var html = await GetHtmlAsync(url, cancellationToken);

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var channelInfo = new TelegramChannelInfo
            {
                Username = channelUsername
            };

            // Назва каналу
            var titleNode = doc.DocumentNode.SelectSingleNode("//div[@class='tgme_channel_info_header_title']");
            if (titleNode != null)
            {
                channelInfo.Title = titleNode.InnerText.Trim();
            }

            // Опис каналу
            var descNode = doc.DocumentNode.SelectSingleNode("//div[@class='tgme_channel_info_description']");
            if (descNode != null)
            {
                channelInfo.Description = descNode.InnerText.Trim();
            }

            // Кількість підписників
            var subscribersNode = doc.DocumentNode.SelectSingleNode("//div[@class='tgme_channel_info_counter']");
            if (subscribersNode != null)
            {
                var subscribersText = subscribersNode.InnerText.Trim();
                channelInfo.SubscribersCount = ParseSubscribersCount(subscribersText);
            }

            // Аватар
            var avatarNode = doc.DocumentNode.SelectSingleNode("//img[@class='tgme_page_photo_image']");
            if (avatarNode != null)
            {
                channelInfo.AvatarUrl = avatarNode.GetAttributeValue("src", string.Empty);
            }

            // Верифікація
            var verifiedNode = doc.DocumentNode.SelectSingleNode("//i[@class='verified-icon']");
            channelInfo.IsVerified = verifiedNode != null;

            _logger.LogInformation("Отримано інформацію про канал @{Username}", channelUsername);

            return channelInfo;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Помилка при отриманні інформації про канал @{Username}", channelUsername);
            return null;
        }
    }

    public async Task<List<TelegramChannelMessage>> GetLatestMessagesAsync(
        string channelUsername, 
        int count = 10, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"{TelegramBaseUrl}{channelUsername}";
            var html = await GetHtmlAsync(url, cancellationToken);

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var messages = new List<TelegramChannelMessage>();

            // Знаходимо всі повідомлення - виключаємо підкласи з "_"
            var messageNodes = doc.DocumentNode.SelectNodes(
                "//div[contains(@class, 'tgme_widget_message') and not(contains(@class, 'tgme_widget_message_'))]");

            if (messageNodes == null)
            {
                _logger.LogWarning("Не знайдено повідомлень у каналі @{Username}", channelUsername);
                return messages;
            }

            // Беремо ОСТАННІ N повідомлень (найновіші - знизу в HTML)
            var latestNodes = messageNodes.Skip(Math.Max(0, messageNodes.Count - count)).ToList();

            foreach (var node in latestNodes)
            {
                var message = ParseMessage(node, channelUsername);
                if (message != null)
                {
                    messages.Add(message);
                }
            }

            // Сортуємо по даті публікації від найновіших (для консистентності)
            messages = messages
                .OrderByDescending(m => m.PublishedAt)
                .ToList();

            _logger.LogInformation("Отримано {Count} останніх повідомлень з каналу @{Username}", messages.Count, channelUsername);

            return messages;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Помилка при отриманні повідомлень з каналу @{Username}", channelUsername);
            return new List<TelegramChannelMessage>();
        }
    }

    public async Task<TelegramChannelMessage?> GetMessageAsync(
        string channelUsername, 
        long messageId, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"{TelegramBaseUrl}{channelUsername}/{messageId}";
            var html = await GetHtmlAsync(url, cancellationToken);

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // Виключаємо підкласи з "_"
            var messageNode = doc.DocumentNode.SelectSingleNode(
                "//div[contains(@class, 'tgme_widget_message') and not(contains(@class, 'tgme_widget_message_'))]");

            if (messageNode == null)
            {
                _logger.LogWarning("Повідомлення {MessageId} не знайдено у каналі @{Username}", messageId, channelUsername);
                return null;
            }

            var message = ParseMessage(messageNode, channelUsername);

            _logger.LogInformation("Отримано повідомлення {MessageId} з каналу @{Username}", messageId, channelUsername);

            return message;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Помилка при отриманні повідомлення {MessageId} з каналу @{Username}", messageId, channelUsername);
            return null;
        }
    }

    public async Task<List<TelegramChannelMessage>> GetMessagesByDateRangeAsync(
        string channelUsername, 
        DateTime from, 
        DateTime to, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Отримуємо більше повідомлень і фільтруємо за датою
            var allMessages = await GetLatestMessagesAsync(channelUsername, 50, cancellationToken);

            var filteredMessages = allMessages
                .Where(m => m.PublishedAt >= from && m.PublishedAt <= to)
                .OrderByDescending(m => m.PublishedAt)
                .ToList();

            _logger.LogInformation("Знайдено {Count} повідомлень у каналі @{Username} за період {From} - {To}", 
                filteredMessages.Count, channelUsername, from, to);

            return filteredMessages;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Помилка при отриманні повідомлень за датою з каналу @{Username}", channelUsername);
            return new List<TelegramChannelMessage>();
        }
    }

    private async Task<string> GetHtmlAsync(string url, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("User-Agent", UserAgent);
        
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    private TelegramChannelMessage? ParseMessage(HtmlNode messageNode, string channelUsername)
    {
        try
        {
            var message = new TelegramChannelMessage();

            // ID повідомлення
            var dataPost = messageNode.GetAttributeValue("data-post", "");
            if (!string.IsNullOrEmpty(dataPost))
            {
                var parts = dataPost.Split('/');
                if (parts.Length > 1 && long.TryParse(parts[1], out var messageId))
                {
                    message.MessageId = messageId;
                    message.Url = $"https://t.me/{channelUsername}/{messageId}";
                }
            }

            // Дата публікації
            var timeNode = messageNode.SelectSingleNode(".//time[@datetime]");
            if (timeNode != null)
            {
                var datetime = timeNode.GetAttributeValue("datetime", "");
                if (DateTime.TryParse(datetime, null, DateTimeStyles.RoundtripKind, out var publishedAt))
                {
                    message.PublishedAt = publishedAt;
                }
            }

            // Текст повідомлення
            var textNode = messageNode.SelectSingleNode(".//div[contains(@class, 'tgme_widget_message_text')]");
            if (textNode != null)
            {
                // Замінюємо <br> на \n перед отриманням тексту
                var innerHTML = textNode.InnerHtml;
                innerHTML = innerHTML.Replace("<br>", "\n").Replace("<br/>", "\n").Replace("<br />", "\n");
                
                // Створюємо тимчасовий вузол для декодування HTML entities
                var tempDoc = new HtmlDocument();
                tempDoc.LoadHtml(innerHTML);
                
                message.Text = HtmlEntity.DeEntitize(tempDoc.DocumentNode.InnerText.Trim());
            }

            // Перегляди
            var viewsNode = messageNode.SelectSingleNode(".//span[contains(@class, 'tgme_widget_message_views')]");
            if (viewsNode != null)
            {
                var viewsText = viewsNode.InnerText.Trim();
                message.Views = ParseViews(viewsText);
            }

            // Медіа
            var photoNodes = messageNode.SelectNodes(".//a[contains(@class, 'tgme_widget_message_photo_wrap')]");
            if (photoNodes != null && photoNodes.Count > 0)
            {
                message.HasMedia = true;
                foreach (var photoNode in photoNodes)
                {
                    var style = photoNode.GetAttributeValue("style", "");
                    var urlMatch = Regex.Match(style, @"background-image:url\('([^']+)'\)");
                    if (urlMatch.Success)
                    {
                        message.MediaUrls.Add(urlMatch.Groups[1].Value);
                    }
                }
            }

            return message;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Помилка при парсингу повідомлення");
            return null;
        }
    }

    private int? ParseSubscribersCount(string text)
    {
        try
        {
            // Приклади: "1.2K", "10K", "1M", "500"
            var match = Regex.Match(text, @"([\d.]+)\s*([KMB])?");
            if (match.Success)
            {
                var number = double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
                var suffix = match.Groups[2].Value;

                var multiplier = suffix switch
                {
                    "K" => 1000,
                    "M" => 1000000,
                    "B" => 1000000000,
                    _ => 1
                };

                return (int)(number * multiplier);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не вдалося розпарсити кількість підписників: {Text}", text);
        }

        return null;
    }

    private int? ParseViews(string text)
    {
        try
        {
            // Приклади: "1.2K", "10K", "1M"
            var match = Regex.Match(text, @"([\d.]+)\s*([KMB])?");
            if (match.Success)
            {
                var number = double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
                var suffix = match.Groups[2].Value;

                var multiplier = suffix switch
                {
                    "K" => 1000,
                    "M" => 1000000,
                    "B" => 1000000000,
                    _ => 1
                };

                return (int)(number * multiplier);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не вдалося розпарсити кількість переглядів: {Text}", text);
        }

        return null;
    }
}
