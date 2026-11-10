using AVE.ElectricityBot.Data.Entities;
using AVE.ElectricityBot.Services.Implementations;
using AVE.ElectricityBot.TelegramScraper.Models;

namespace AVE.ElectricityBot.Tests;

/// <summary>
/// Вибір нових постів каналу відносно збереженого стану
/// </summary>
public class ChannelScraperStateTests
{
    private static TelegramChannelMessage Post(long id, DateTime publishedUtc) =>
        new() { MessageId = id, PublishedAt = publishedUtc };

    private static ChannelScrapingState State(long lastId, DateTime lastPublishedUtc) =>
        new() { ChannelUsername = "ddnepr_vse", LastProcessedMessageId = lastId, LastProcessedAt = lastPublishedUtc };

    [Fact]
    public void WithoutStateAllPostsAreNew()
    {
        var posts = new List<TelegramChannelMessage> { Post(1, DateTime.UtcNow), Post(2, DateTime.UtcNow) };

        Assert.Equal(posts, ChannelScraperService.SelectNewMessages(posts, null));
    }

    [Fact]
    public void OnlyPostsAfterLastProcessedAreNew()
    {
        var last = new DateTime(2026, 11, 9, 20, 0, 0, DateTimeKind.Utc);
        var posts = new List<TelegramChannelMessage>
        {
            Post(99, last.AddMinutes(-30)),
            Post(100, last),
            Post(101, last.AddMinutes(5)),
        };

        var result = ChannelScraperService.SelectNewMessages(posts, State(100, last));

        Assert.Equal([101L], result.Select(p => p.MessageId));
    }

    /// <summary>
    /// @ddnepr_vse перестворили в грудні 2025: збережений ID ~15663, а нові пости мають ID ~7457.
    /// Лише перевірка за ID відкидала всі нові пости
    /// </summary>
    [Fact]
    public void PostsAfterChannelIdResetAreNew()
    {
        var lastProcessed = new DateTime(2025, 12, 2, 11, 0, 0, DateTimeKind.Utc);
        var posts = new List<TelegramChannelMessage>
        {
            Post(7455, new DateTime(2026, 11, 9, 21, 16, 0, DateTimeKind.Utc)),
            Post(7457, new DateTime(2026, 11, 9, 22, 2, 0, DateTimeKind.Utc)),
        };

        var result = ChannelScraperService.SelectNewMessages(posts, State(15663, lastProcessed));

        Assert.Equal([7455L, 7457L], result.Select(p => p.MessageId));
    }

    [Fact]
    public void LocalPublishedTimeIsComparedInUtc()
    {
        var lastUtc = new DateTime(2026, 11, 9, 20, 0, 0, DateTimeKind.Utc);
        var olderLocal = lastUtc.AddMinutes(-1).ToLocalTime();

        var result = ChannelScraperService.SelectNewMessages([Post(50, olderLocal)], State(100, lastUtc));

        Assert.Empty(result);
    }
}
