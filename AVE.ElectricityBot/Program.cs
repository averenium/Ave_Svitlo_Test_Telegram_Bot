using AVE.ElectricityBot.Configuration;
using AVE.ElectricityBot.Data.Extensions;
using AVE.ElectricityBot.Services.Extensions;
using AVE.ElectricityBot.YasnoApi.Extensions;
using AVE.ElectricityBot.TelegramScraper;
using Microsoft.Extensions.Options;
using System.Text;
using Telegram.Bot;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using AVE.ElectricityBot.Data.Context;
using AVE.ElectricityBot.Data.Services;
using Microsoft.EntityFrameworkCore;

// Налаштування кодування для консолі (для правильного відображення емодзі в логах)
Console.OutputEncoding = Encoding.UTF8;
Console.InputEncoding = Encoding.UTF8;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.Configure<TelegramBotConfiguration>(
    builder.Configuration.GetSection("TelegramBot"));

builder.Services.Configure<YasnoAddressConfiguration>(
    builder.Configuration.GetSection("YasnoAddress"));

// Реєстрація Telegram Bot Client
builder.Services.AddSingleton<ITelegramBotClient>(sp =>
{
    var config = sp.GetRequiredService<IOptions<TelegramBotConfiguration>>().Value;

    if (string.IsNullOrEmpty(config.Token))
        throw new ArgumentException("Telegram Bot Token не налаштовано");

    return new TelegramBotClient(config.Token);
});

// Використовуємо extension метод для додавання PostgreSQL (автоматично читає конфігурацію)
builder.Services.AddPostgresData();

// Додаємо Yasno API клієнт
builder.Services.AddYasnoApiClient();

// Додаємо Telegram Channel Scraper
builder.Services.AddTelegramChannelScraper();

// Додаємо сервіси бота (використовуючи нову DI архітектуру)
builder.Services.AddElectricityBotServices();

// Додаємо OpenTelemetry з Prometheus для метрик
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(
            serviceName: "AVE.ElectricityBot",
            serviceVersion: "1.0.0",
            serviceInstanceId: Environment.MachineName))
    .WithMetrics(metrics => metrics
        // ASP.NET Core метрики (HTTP requests/responses)
        .AddAspNetCoreInstrumentation()
        // HTTP Client метрики (вихідні запити)
        .AddHttpClientInstrumentation()
        // Runtime метрики (.NET GC, ThreadPool, etc.)
        .AddRuntimeInstrumentation()
        // Prometheus exporter
        .AddPrometheusExporter());

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddHealthChecks();
var app = builder.Build();
// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapHealthChecks("/health");

// Prometheus metrics endpoint
app.MapPrometheusScrapingEndpoint();

app.UseAuthorization();

app.MapControllers();

app.Run();

