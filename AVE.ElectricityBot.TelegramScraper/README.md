# AVE.ElectricityBot.TelegramScraper

Бібліотека для web scraping публічних Telegram каналів через `t.me/s/` preview.

## ?? Можливості

- ? Отримання інформації про канал
- ? Читання останніх N повідомлень
- ? Отримання конкретного повідомлення за ID
- ? Фільтрація повідомлень за датою
- ? Парсинг переглядів
- ? Інтеграція з DI контейнером


## ?? Налаштування

### Реєстрація в DI

```csharp
// Program.cs
services.AddTelegramChannelScraper();

// або з налаштуваннями HttpClient
services.AddTelegramChannelScraper(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});
```

## ?? Використання

### Отримання інформації про канал

```csharp
var scraper = serviceProvider.GetRequiredService<ITelegramChannelScraper>();

var channelInfo = await scraper.GetChannelInfoAsync("test");

Console.WriteLine($"Назва: {channelInfo.Title}");
Console.WriteLine($"Підписників: {channelInfo.SubscribersCount}");
Console.WriteLine($"Верифікований: {channelInfo.IsVerified}");
```

### Отримання останніх повідомлень

```csharp
var messages = await scraper.GetLatestMessagesAsync("test", count: 10);

foreach (var message in messages)
{
    Console.WriteLine($"[{message.PublishedAt}] {message.Text}");
    Console.WriteLine($"Перегляди: {message.Views}");
    Console.WriteLine($"Посилання: {message.Url}");
    Console.WriteLine();
}
```

### Отримання конкретного повідомлення

```csharp
var message = await scraper.GetMessageAsync("test", messageId: 12345);

if (message != null)
{
    Console.WriteLine($"Текст: {message.Text}");
    Console.WriteLine($"Дата: {message.PublishedAt}");
    
    if (message.HasMedia)
    {
        Console.WriteLine($"Медіа ({message.MediaUrls.Count}):");
        foreach (var url in message.MediaUrls)
        {
            Console.WriteLine($"  - {url}");
        }
    }
}
```

### Фільтрація за датою

```csharp
var from = DateTime.Now.AddDays(-7);
var to = DateTime.Now;

var messages = await scraper.GetMessagesByDateRangeAsync(
    "dtek_official",
    from,
    to
);

Console.WriteLine($"Знайдено {messages.Count} повідомлень за тиждень");
```

## ?? Моделі даних

### TelegramChannelInfo

```csharp
public class TelegramChannelInfo
{
    public string Username { get; set; }
    public string Title { get; set; }
    public string? Description { get; set; }
    public int? SubscribersCount { get; set; }
    public string? AvatarUrl { get; set; }
    public bool IsVerified { get; set; }
}
```

### TelegramChannelMessage

```csharp
public class TelegramChannelMessage
{
    public long MessageId { get; set; }
    public DateTime PublishedAt { get; set; }
    public string Text { get; set; }
    public string Url { get; set; }
    public int? Views { get; set; }
    public bool HasMedia { get; set; }
    public List<string> MediaUrls { get; set; }
}
```

## ?? Обмеження

1. **Не офіційний API** - використовує публічний preview Telegram
2. **Rate limiting** - Telegram може обмежити кількість запитів
3. **Структура HTML** - може змінитися в будь-який момент
4. **Тільки публічні канали** - приватні канали недоступні
5. **Обмежена історія** - доступно ~50 останніх повідомлень

**Увага:** Ця бібліотека використовує web scraping та не є офіційним API Telegram. Використовуйте відповідально!
