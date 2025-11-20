using AVE.ElectricityBot.Configuration;
using AVE.ElectricityBot.Data.Abstractions;
using AVE.ElectricityBot.Services.Interfaces;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Bot.Polling;

namespace AVE.ElectricityBot.Services;

public class TelegramMessageService : BackgroundService
{
    private readonly ILogger<TelegramMessageService> _logger;
    private readonly ITelegramBotClient _botClient;
    private readonly string _chatId;
    private readonly IEnumerable<ICommandHandler> _commandHandlers;
    private readonly ICallbackQueryHandler _callbackQueryHandler;
    private readonly IKeyboardService _keyboardService;
    private readonly INotificationService _notificationService;
    private readonly IBotDataService _dataService;

    // Інтервал перевірки
    private readonly TimeSpan _statusCheckInterval = TimeSpan.FromMinutes(1);

    public TelegramMessageService(
        ILogger<TelegramMessageService> logger,
        ITelegramBotClient botClient,
        IOptions<TelegramBotConfiguration> config,
        IEnumerable<ICommandHandler> commandHandlers,
        ICallbackQueryHandler callbackQueryHandler,
        IKeyboardService keyboardService,
        INotificationService notificationService,
        IBotDataService dataService)
    {
        _logger = logger;
        _botClient = botClient;
        _commandHandlers = commandHandlers;
        _callbackQueryHandler = callbackQueryHandler;
        _keyboardService = keyboardService;
        _notificationService = notificationService;
        _dataService = dataService;
        _chatId = string.Empty; // Більше не використовується
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Telegram Message Service запущено");

        try
        {
            var me = await _botClient.GetMe(stoppingToken);
            _logger.LogInformation("Бот підключено: @{BotUsername}", me.Username);

            // Реєстрація команд
            await RegisterBotCommandsAsync(stoppingToken);

            // Запуск обробки повідомлень
            StartReceivingMessages(stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Помилка підключення до Telegram бота");
            return;
        }

        // Початкове повідомлення
        await _notificationService.CheckAndSendNotificationAsync(forceStartup: true, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _notificationService.CheckAndSendNotificationAsync(forceStartup: false, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка при перевірці змін");
            }

            await Task.Delay(_statusCheckInterval, stoppingToken);
        }
    }

    private async Task RegisterBotCommandsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var commands = _commandHandlers
                .Select(h => new BotCommand 
                { 
                    Command = h.CommandName, 
                    Description = h.Description 
                })
                .ToArray();

            await _botClient.SetMyCommands(commands, cancellationToken: cancellationToken);
            _logger.LogInformation("Команди бота зареєстровано: {Count} команд", commands.Length);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Помилка при реєстрації команд");
        }
    }

    private void StartReceivingMessages(CancellationToken cancellationToken)
    {
        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = new[] { UpdateType.Message, UpdateType.CallbackQuery }
        };

        _botClient.StartReceiving(
            HandleUpdateAsync,
            HandleErrorAsync,
            receiverOptions,
            cancellationToken);

        _logger.LogInformation("Обробка команд активована");
    }

    private Task HandleErrorAsync(
        ITelegramBotClient botClient,
        Exception exception,
        CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "Помилка Telegram бота");
        return Task.CompletedTask;
    }

    private async Task HandleUpdateAsync(
        ITelegramBotClient botClient,
        Update update,
        CancellationToken cancellationToken)
    {
        // Обробка callback запитів (натискання кнопок)
        if (update.CallbackQuery is { } callbackQuery)
        {
            await HandleCallbackQueryAsync(botClient, callbackQuery, cancellationToken);
            return;
        }

        // Обробка текстових повідомлень
        if (update.Message is not { } message)
            return;

        if (message.Text is not { } messageText)
            return;

        _logger.LogInformation("Отримано команду: {Command} від {Username}",
            messageText, message.From?.Username ?? "Unknown");

        try
        {
            // Зберігаємо/оновлюємо користувача
            await _dataService.GetOrCreateUserAsync(
                telegramUserId: message.From!.Id,
                telegramChatId: message.Chat.Id,
                username: message.From.Username,
                firstName: message.From.FirstName,
                lastName: message.From.LastName,
                cancellationToken: cancellationToken);

            // Визначаємо команду (без /)
            var commandName = messageText.ToLower().TrimStart('/').Split(' ')[0];
            
            // Шукаємо обробник команди
            var handler = _commandHandlers.FirstOrDefault(h => h.CommandName == commandName);
            
            string response;
            InlineKeyboardMarkup? keyboard;

            if (handler != null)
            {
                // Виконуємо команду через handler
                response = await handler.HandleAsync(message, cancellationToken);
                
                // Визначаємо клавіатуру
                keyboard = commandName switch
                {
                    "start" or "menu" => _keyboardService.GetMainMenu(),
                    "status" or "today" or "tomorrow" or "group" => _keyboardService.GetBackToMainMenu(),
                    "schedule" => _keyboardService.GetScheduleMenu(),
                    "other" when messageText.Split(' ').Length == 1 => _keyboardService.GetGroupSelectionMenu(),
                    "other" => _keyboardService.GetBackToMainMenu(),
                    "help" => _keyboardService.GetMainMenu(),
                    _ => _keyboardService.GetMainMenu()
                };
            }
            else
            {
                response = "❓ Невідомо команда. Використовуйте /menu для відкриття меню.";
                keyboard = _keyboardService.GetMainMenu();
            }

            await botClient.SendMessage(
                chatId: message.Chat.Id,
                text: response,
                parseMode: ParseMode.Html,
                replyMarkup: keyboard,
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Помилка обробки команди: {Command}", messageText);
            //await botClient.SendMessage(
            //    chatId: message.Chat.Id,
            //    text: $"❌ Помилка: {ex.Message}",
            //    replyMarkup: _keyboardService.GetMainMenu(),
            //    cancellationToken: cancellationToken);
        }
    }

    private async Task HandleCallbackQueryAsync(
        ITelegramBotClient botClient,
        CallbackQuery callbackQuery,
        CancellationToken cancellationToken)
    {
        if (callbackQuery.Message is not { } message)
            return;

        try
        {
            // Делегуємо обробку до CallbackQueryHandler
            var (response, keyboard) = await _callbackQueryHandler.HandleAsync(callbackQuery, cancellationToken);

            // Відповідаємо на callback
            await botClient.AnswerCallbackQuery(
                callbackQueryId: callbackQuery.Id,
                cancellationToken: cancellationToken);

            // Редагуємо повідомлення або надсилаємо нове
            try
            {
                await botClient.EditMessageText(
                    chatId: message.Chat.Id,
                    messageId: message.MessageId,
                    text: response,
                    parseMode: ParseMode.Html,
                    replyMarkup: keyboard,
                    cancellationToken: cancellationToken);
            }
            catch
            {
                // Якщо не вдалося відредагувати - надсилаємо нове
                await botClient.SendMessage(
                    chatId: message.Chat.Id,
                    text: response,
                    parseMode: ParseMode.Html,
                    replyMarkup: keyboard,
                    cancellationToken: cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Помилка обробки callback: {Callback}", callbackQuery.Data);
            
            await botClient.AnswerCallbackQuery(
                callbackQueryId: callbackQuery.Id,
                text: "❌ Помилка обробки запиту",
                showAlert: true,
                cancellationToken: cancellationToken);
        }
    }
}
