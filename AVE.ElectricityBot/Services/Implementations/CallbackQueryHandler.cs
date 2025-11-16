using AVE.ElectricityBot.Configuration;
using AVE.ElectricityBot.Data.Interfaces;
using AVE.ElectricityBot.Services.CommandHandlers;
using AVE.ElectricityBot.Services.Interfaces;
using Microsoft.Extensions.Options;
using System.Text;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace AVE.ElectricityBot.Services.Implementations;

/// <summary>
/// Обробник callback-запитів від інлайн-кнопок
/// </summary>
public class CallbackQueryHandler : ICallbackQueryHandler
{
    private readonly IEnumerable<ICommandHandler> _commandHandlers;
    private readonly IKeyboardService _keyboardService;
    private readonly IBotDataService _dataService;
    private readonly ILogger<CallbackQueryHandler> _logger;
    private readonly IScheduleCacheService _cacheService;
    private readonly IMessageFormatterService _messageFormatter;

    public CallbackQueryHandler(
        IEnumerable<ICommandHandler> commandHandlers,
        IKeyboardService keyboardService,
        IBotDataService dataService,
        ILogger<CallbackQueryHandler> logger,
        IScheduleCacheService cacheService,
        IMessageFormatterService messageFormatter)
    {
        _commandHandlers = commandHandlers;
        _keyboardService = keyboardService;
        _dataService = dataService;
        _logger = logger;
        _cacheService = cacheService;
        _messageFormatter = messageFormatter;
    }

    public async Task<(string response, InlineKeyboardMarkup? keyboard)> HandleAsync(
        CallbackQuery callbackQuery,
        CancellationToken cancellationToken)
    {
        var callbackData = callbackQuery.Data ?? "";
        
        _logger.LogInformation("Обробка callback: {CallbackData}", callbackData);

        // Обробка команд (префікс cmd_)
        if (callbackData.StartsWith("cmd_"))
        {
            return await HandleCommandCallbackAsync(callbackData, callbackQuery.Message!, callbackQuery, cancellationToken);
        }
        
        // Обробка вибору групи для other
        if (callbackData.StartsWith("other_"))
        {
            return await HandleOtherGroupCallbackAsync(callbackData, callbackQuery.Message!, cancellationToken);
        }

        // Обробка встановлення власної групи
        if (callbackData.StartsWith("setgroup_"))
        {
            return await HandleSetGroupCallbackAsync(callbackData, callbackQuery, cancellationToken);
        }

        // Обробка вибору DSO
        if (callbackData.StartsWith("selectdso_"))
        {
            return await HandleSelectDsoCallbackAsync(callbackData, cancellationToken);
        }

        // Обробка кнопки "Моя група"
        if (callbackData == "mygroup")
        {
            return await HandleMyGroupCallbackAsync(callbackQuery, cancellationToken);
        }

        // Обробка кнопки "Змінити групу"
        if (callbackData == "changegroup")
        {
            return await HandleChangeGroupCallbackAsync(cancellationToken);
        }

        // Обробка налаштувань
        if (callbackData.StartsWith("settings_"))
        {
            return await HandleSettingsCallbackAsync(callbackData, callbackQuery, cancellationToken);
        }

        return ("❓ Невідома дія", _keyboardService.GetMainMenu());
    }

    private async Task<(string response, InlineKeyboardMarkup? keyboard)> HandleCommandCallbackAsync(
        string callbackData,
        Message message,
        CallbackQuery callbackQuery,
        CancellationToken cancellationToken)
    {
        // Визначаємо команду з callback data
        var commandName = callbackData["cmd_".Length..];
        
        // Спеціальна обробка для cmd_main
        if (commandName == "main")
        {
            return ("📋 <b>ГОЛОВНЕ МЕНЮ</b>\n\nОберіть дію:", _keyboardService.GetMainMenu());
        }

        // Спеціальна обробка для cmd_settings
        if (commandName == "settings")
        {
            return await HandleSettingsViewCallbackAsync(callbackQuery, cancellationToken);
        }

        // Спеціальна обробка для cmd_other (показуємо меню вибору провайдера спочатку)
        if (commandName == "other")
        {
            return ("🔍 <b>ІНША ГРУПА</b>\n\n" +
                    "Спочатку оберіть провайдера:\n\n" +
                    "• <b>ДТЕК</b> - для перегляду груп ДТЕК\n" +
                    "• <b>ЦЕК</b> - для перегляду груп ЦЕК",
                    _keyboardService.GetDsoSelectionMenuForOther());
        }

        // Для команд що потребують групу - перевіряємо чи встановлена
        if (commandName is "status" or "today" or "tomorrow" or "schedule")
        {
            var users = await _dataService.GetActiveUsersAsync(cancellationToken);
            var user = users.FirstOrDefault(u => u.TelegramUserId == callbackQuery.From.Id);

            if (user == null || string.IsNullOrEmpty(user.GroupName))
            {
                return ("📍 <b>ВСТАНОВІТЬ ВАШУ ГРУПУ</b>\n\n" +
                        "Для отримання інформації про відключення спочатку оберіть вашу групу.\n\n" +
                        "Виберіть групу відключень для вашої адреси:", 
                        _keyboardService.GetMyGroupSelectionMenu());
            }
        }

        // Шукаємо обробник команди
        var handler = _commandHandlers.FirstOrDefault(h => h.CommandName == commandName);
        
        if (handler == null)
        {
            _logger.LogWarning("Обробник для команди {CommandName} не знайдено", commandName);
            return ("❓ Невідома команда", _keyboardService.GetMainMenu());
        }

        // Створюємо Message з правильним From для обробника
        var messageForHandler = new Message
        {
            Chat = message.Chat,
            From = callbackQuery.From // ✅ Беремо From з callbackQuery!
        };

        // Виконуємо команду
        var response = await handler.HandleAsync(messageForHandler, cancellationToken);
        
        // Визначаємо яку клавіатуру показати
        var keyboard = commandName switch
        {
            "status" => _keyboardService.GetBackToMainMenu(),
            "schedule" => _keyboardService.GetScheduleMenu(),
            "today" or "tomorrow" => _keyboardService.GetBackToMainMenu(),
            "group" => _keyboardService.GetBackToMainMenu(),
            "help" => _keyboardService.GetMainMenu(),
            "menu" or "start" => _keyboardService.GetMainMenu(),
            _ => _keyboardService.GetMainMenu()
        };

        return (response, keyboard);
    }

    private async Task<(string response, InlineKeyboardMarkup? keyboard)> HandleOtherGroupCallbackAsync(
        string callbackData,
        Message message,
        CancellationToken cancellationToken)
    {
        // Формат: other_301_5_1 (з DSO) або other_5_1 (старий формат)
        var parts = callbackData.Split('_');
        if (parts.Length < 3)
        {
            return ("❌ Невірний формат вибору групи", _keyboardService.GetGroupSelectionMenu());
        }

        int dsoId;
        string group;
        string subgroup;

        if (parts.Length == 4)
        {
            // Новий формат: other_301_5_1
            if (!int.TryParse(parts[1], out dsoId))
            {
                return ("❌ Невірний формат DSO", _keyboardService.GetMainMenu());
            }
            group = parts[2];
            subgroup = parts[3];
        }
        else
        {
            // Старий формат: other_5_1 - використовуємо DSO за замовчуванням
            dsoId = 301; // ДТЕК за замовчуванням
            group = parts[1];
            subgroup = parts[2];
        }

        // Отримуємо розклад з кешу для конкретного DSO
        var outages = await _cacheService.GetPlannedOutagesAsync(dsoId, cancellationToken);

        if (outages == null)
        {
            var dsoName = dsoId == 301 ? "ДТЕК" : "ЦЕК";
            return ($"❌ Не вдалося отримати розклад для {dsoName}", _keyboardService.GetMainMenu());
        }

        // Якщо subgroup == "both" - показуємо обидві підгрупи
        if (subgroup == "both")
        {
            var schedule1 = outages.GetSchedule(int.Parse(group), 1);
            var schedule2 = outages.GetSchedule(int.Parse(group), 2);

            if (schedule1 == null && schedule2 == null)
            {
                return ($"❌ Розклад для групи {group} не знайдено", _keyboardService.GetBackToMainMenu());
            }

            var dsoName = dsoId == 301 ? "ДТЕК" : "ЦЕК";
            var sb = new StringBuilder();
            sb.AppendLine($"🔍 <b>ГРУПА {group}</b>");
            sb.AppendLine($"⚡ Провайдер: <b>{dsoName}</b>");
            sb.AppendLine();

            if (schedule1 != null)
            {
                sb.AppendLine($"📍 <b>Підгрупа {group}.1</b>");
                sb.AppendLine("┌─ 📅 <b>СЬОГОДНІ</b> ─────────────");
                _messageFormatter.AppendDailyScheduleSimple(sb, schedule1.Today);
                sb.AppendLine("└────────────────────────");
                sb.AppendLine();
            }

            if (schedule2 != null)
            {
                sb.AppendLine($"📍 <b>Підгрупа {group}.2</b>");
                sb.AppendLine("┌─ 📅 <b>СЬОГОДНІ</b> ─────────────");
                _messageFormatter.AppendDailyScheduleSimple(sb, schedule2.Today);
                sb.AppendLine("└────────────────────────");
            }

            return (sb.ToString(), _keyboardService.GetBackToMainMenu());
        }
        else
        {
            // Показуємо одну підгрупу
            var schedule = outages.GetSchedule(int.Parse(group), int.Parse(subgroup));

            if (schedule == null)
            {
                return ($"❌ Розклад для групи {group}.{subgroup} не знайдено", 
                        _keyboardService.GetBackToMainMenu());
            }

            var dsoName = dsoId == 301 ? "ДТЕК" : "ЦЕК";
            var sb = new StringBuilder();
            sb.AppendLine($"🔍 <b>ГРУПА {group}.{subgroup}</b>");
            sb.AppendLine($"⚡ Провайдер: <b>{dsoName}</b>");
            sb.AppendLine();
            sb.AppendLine("┌─ 📅 <b>СЬОГОДНІ</b> ─────────────");
            _messageFormatter.AppendDailyScheduleSimple(sb, schedule.Today);
            sb.AppendLine("└────────────────────────");
            sb.AppendLine();
            sb.AppendLine("┌─ 📅 <b>ЗАВТРА</b> ───────────────");
            _messageFormatter.AppendDailyScheduleSimple(sb, schedule.Tomorrow);
            sb.AppendLine("└────────────────────────");

            return (sb.ToString(), _keyboardService.GetBackToMainMenu());
        }
    }

    private async Task<(string response, InlineKeyboardMarkup? keyboard)> HandleSetGroupCallbackAsync(
        string callbackData,
        CallbackQuery callbackQuery,
        CancellationToken cancellationToken)
    {
        // Формат: setgroup_301_5.1 або setgroup_5.1 (старий формат)
        var parts = callbackData["setgroup_".Length..].Split('_');
        
        int dsoId;
        string groupName;

        if (parts.Length == 2)
        {
            // Новий формат: setgroup_301_5.1
            if (!int.TryParse(parts[0], out dsoId))
            {
                return ("❌ Невірний формат DSO", _keyboardService.GetMainMenu());
            }
            groupName = parts[1];
        }
        else
        {
            // Старий формат: setgroup_5.1 - використовуємо DSO з користувача або за замовчуванням
            groupName = parts[0];
            var users = await _dataService.GetActiveUsersAsync(cancellationToken);
            var user = users.FirstOrDefault(u => u.TelegramUserId == callbackQuery.From.Id);
            dsoId = user?.DsoId ?? 301; // За замовчуванням ДТЕК
        }

        try
        {
            // Зберігаємо групу та DSO користувача
            await _dataService.UpdateUserGroupAndDsoAsync(
                callbackQuery.From.Id,
                groupName,
                dsoId,
                cancellationToken);

            // Отримуємо розклад з кешу
            var outages = await _cacheService.GetPlannedOutagesAsync(dsoId, cancellationToken);

            if (outages == null)
            {
                var dsoName = dsoId == 301 ? "ДТЕК" : "ЦЕК";
                return ($"✅ <b>Налаштовано!</b>\n\n" +
                        $"Провайдер: <b>{dsoName}</b>\n" +
                        $"Група: <code>{groupName}</code>\n\n" +
                        "Тепер ви будете отримувати інформацію для вашої групи.\n\n" +
                        "Використовуйте:\n" +
                        "• /status - поточний статус\n" +
                        "• /today - розклад на сьогодні\n" +
                        "• /tomorrow - розклад на завтра",
                        _keyboardService.GetMainMenu());
            }

            var groupParts = groupName.Split('.');
            var schedule = outages.GetSchedule(int.Parse(groupParts[0]), int.Parse(groupParts[1]));

            if (schedule == null)
            {
                return ($"✅ <b>Налаштовано!</b>\n\n" +
                        $"Група: <code>{groupName}</code>\n\n" +
                        "Розклад для цієї групи наразі недоступний.",
                        _keyboardService.GetMainMenu());
            }

            // Показуємо розклад
            var dsoNameFull = dsoId == 301 ? "ДТЕК" : "ЦЕК";
            var sb = new StringBuilder();
            sb.AppendLine($"✅ <b>Налаштовано!</b>");
            sb.AppendLine();
            sb.AppendLine($"📍 Група: <code>{groupName}</code> ({dsoNameFull})");
            sb.AppendLine();
            sb.AppendLine("📅 <b>Розклад на сьогодні:</b>");
            sb.AppendLine();
            _messageFormatter.AppendDailyScheduleSimple(sb, schedule.Today);
            sb.AppendLine();
            sb.AppendLine("Використовуйте /status для детальної інформації");

            return (sb.ToString(), _keyboardService.GetMainMenu());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Помилка при встановленні групи {GroupName}", groupName);
            return ("❌ Помилка при встановленні групи. Спробуйте пізніше.", _keyboardService.GetMainMenu());
        }
    }

    private Task<(string response, InlineKeyboardMarkup? keyboard)> HandleSelectDsoCallbackAsync(
        string callbackData,
        CancellationToken cancellationToken)
    {
        // Формат: selectdso_301 або selectdso_other_301
        var parts = callbackData["selectdso_".Length..].Split('_');
        
        bool isForOther = parts.Length > 1 && parts[0] == "other";
        var dsoIdStr = isForOther ? parts[1] : parts[0];
        
        if (!int.TryParse(dsoIdStr, out var dsoId))
        {
            return Task.FromResult<(string, InlineKeyboardMarkup?)>(
                ("❌ Невірний формат DSO", _keyboardService.GetMainMenu()));
        }

        var dsoName = dsoId == 301 ? "ДТЕК" : "ЦЕК";
        
        if (isForOther)
        {
            // Для перегляду інших груп
            return Task.FromResult<(string, InlineKeyboardMarkup?)>(
                ($"🔍 <b>Провайдер: {dsoName}</b>\n\n" +
                 "Оберіть групу для перегляду розкладу:",
                 _keyboardService.GetGroupSelectionMenuForOther(dsoId)));
        }
        else
        {
            // Для встановлення своєї групи
            return Task.FromResult<(string, InlineKeyboardMarkup?)>(
                ($"⚡ <b>Провайдер: {dsoName}</b>\n\n" +
                 "Тепер оберіть вашу групу відключень:",
                 _keyboardService.GetGroupSelectionMenuForDso(dsoId)));
        }
    }

    private async Task<(string response, InlineKeyboardMarkup? keyboard)> HandleMyGroupCallbackAsync(
        CallbackQuery callbackQuery,
        CancellationToken cancellationToken)
    {
        try
        {
            // Отримуємо користувача з БД
            var users = await _dataService.GetActiveUsersAsync(cancellationToken);
            var user = users.FirstOrDefault(u => u.TelegramUserId == callbackQuery.From.Id);

            if (user != null && !string.IsNullOrEmpty(user.GroupName) && user.DsoId.HasValue)
            {
                // У користувача вже є група та DSO - показуємо статус
                var parts = user.GroupName.Split('.');
                
                // Отримуємо розклад з кешу
                var outages = await _cacheService.GetPlannedOutagesAsync(user.DsoId.Value, cancellationToken);

                if (outages == null)
                {
                    var dsoName = user.DsoId == 301 ? "ДТЕК" : "ЦЕК";
                    return ($"📍 <b>Ваші налаштування:</b>\n\n" +
                            $"⚡ Провайдер: <b>{dsoName}</b>\n" +
                            $"📍 Група: <code>{user.GroupName}</code>\n\n" +
                            "Розклад наразі недоступний.",
                            _keyboardService.GetMyGroupMenuWithChange());
                }

                var schedule = outages.GetSchedule(int.Parse(parts[0]), int.Parse(parts[1]));

                if (schedule == null)
                {
                    return ($"📍 <b>Ваша група: {user.GroupName}</b>\n\n" +
                            "Розклад наразі недоступний.",
                            _keyboardService.GetMyGroupMenuWithChange());
                }

                var dsoNameFull = user.DsoId == 301 ? "ДТЕК" : "ЦЕК";
                var sb = new StringBuilder();
                sb.AppendLine($"📍 <b>ВАШІ НАЛАШТУВАННЯ</b>");
                sb.AppendLine();
                sb.AppendLine($"📍 Група: <code>{user.GroupName}</code> ({dsoNameFull})");
                sb.AppendLine();
                sb.AppendLine("┌─ 📅 <b>СЬОГОДНІ</b> ─────────────");
                _messageFormatter.AppendDailyScheduleSimple(sb, schedule.Today);
                sb.AppendLine("└────────────────────────");
                sb.AppendLine();
                sb.AppendLine("┌─ 📅 <b>ЗАВТРА</b> ───────────────");
                _messageFormatter.AppendDailyScheduleSimple(sb, schedule.Tomorrow);
                sb.AppendLine("└────────────────────────");

                return (sb.ToString(), _keyboardService.GetMyGroupMenuWithChange());
            }
            else
            {
                // Користувач ще не встановив групу - показуємо меню вибору DSO
                return ("⚡ <b>ОБЕРІТЬ ВАШОГО ПРОВАЙДЕРА</b>\n\n" +
                        "Спочатку оберіть вашу електропостачальну компанію:\n\n" +
                        "• <b>ДТЕК</b> - для клієнтів ДТЕК\n" +
                        "• <b>ЦЕК</b> - для клієнтів ЦЕК (Центренерго)\n\n" +
                        "Після цього ви зможете обрати групу відключень.",
                        _keyboardService.GetDsoSelectionMenu());
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Помилка при обробці 'Моя група'");
            return ("❌ Помилка при отриманні інформації", _keyboardService.GetMainMenu());
        }
    }

    private Task<(string response, InlineKeyboardMarkup? keyboard)> HandleChangeGroupCallbackAsync(
        CancellationToken cancellationToken)
    {
        // Показуємо меню вибору провайдера для зміни групи
        return Task.FromResult<(string, InlineKeyboardMarkup?)>(
            ("⚡ <b>ЗМІНИТИ ГРУПУ</b>\n\n" +
             "Оберіть вашого провайдера:\n\n" +
             "• <b>ДТЕК</b> - для клієнтів ДТЕК\n" +
             "• <b>ЦЕК</b> - для клієнтів ЦЕК (Центренерго)\n\n" +
             "Після цього ви зможете обрати нову групу відключень.",
             _keyboardService.GetDsoSelectionMenu()));
    }

    private async Task<(string response, InlineKeyboardMarkup? keyboard)> HandleSettingsViewCallbackAsync(
        CallbackQuery callbackQuery,
        CancellationToken cancellationToken)
    {
        try
        {
            var users = await _dataService.GetActiveUsersAsync(cancellationToken);
            var user = users.FirstOrDefault(u => u.TelegramUserId == callbackQuery.From.Id);

            if (user == null)
            {
                return ("❌ Користувача не знайдено", _keyboardService.GetMainMenu());
            }

            var statusEmoji = user.Settings?.EnableChannelScraping == true ? "✅" : "❌";
            var statusText = user.Settings?.EnableChannelScraping == true ? "Увімкнено" : "Вимкнено";

            var dsoName = user.DsoId switch
            {
                301 => "ДТЕК (Дніпроенерго)",
                303 => "ЦЕК (Центренерго)",
                _ => "Не вказано"
            };

            var warningText = "";
            if (user.Settings?.EnableChannelScraping == true && (string.IsNullOrEmpty(user.GroupName) || !user.DsoId.HasValue))
            {
                warningText = "\n\n⚠️ <b>Увага:</b> Для отримання сповіщень спочатку встановіть вашу групу та провайдера у меню \"Моя група\".";
            }

            var text = $"⚙️ <b>НАЛАШТУВАННЯ</b>\n\n" +
                       $"<b>Поточні параметри:</b>\n" +
                       $"• <b>DSO:</b> {dsoName}\n" +
                       $"• <b>Група:</b> {user.GroupName ?? "Не вказана"}\n\n" +
                       $"<b>Сповіщення з каналів:</b>\n" +
                       $"{statusEmoji} {statusText}\n\n" +
                       $"Коли увімкнено, бот пересилає повідомлення з Telegram каналів (наприклад: @dnipro_svet), " +
                       $"що містять інформацію про ваш провайдер та групу відключень." + warningText;

            return (text, _keyboardService.GetSettingsKeyboard(user.Settings?.EnableChannelScraping ?? false));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Помилка при отриманні налаштувань користувача {UserId}", callbackQuery.From.Id);
            return ("❌ Помилка при отриманні налаштувань", _keyboardService.GetMainMenu());
        }
    }

    private async Task<(string response, InlineKeyboardMarkup? keyboard)> HandleSettingsCallbackAsync(
        string callbackData,
        CallbackQuery callbackQuery,
        CancellationToken cancellationToken)
    {
        try
        {
            bool enableScraping = callbackData == "settings_enable_scraping";

            await _dataService.UpdateUserChannelScrapingAsync(
                callbackQuery.From.Id,
                enableScraping,
                cancellationToken);

            var statusText = enableScraping ? "✅ увімкнено" : "❌ вимкнено";
            var descriptionText = enableScraping
                ? "Тепер ви будете отримувати повідомлення з каналу @dnipro_svet, що стосуються вашого провайдера та групи."
                : "Ви більше не будете отримувати повідомлення з Telegram каналів.";

            // Отримуємо користувача для перевірки налаштувань
            var users = await _dataService.GetActiveUsersAsync(cancellationToken);
            var user = users.FirstOrDefault(u => u.TelegramUserId == callbackQuery.From.Id);

            var warningText = "";
            if (enableScraping && (user == null || string.IsNullOrEmpty(user.GroupName) || !user.DsoId.HasValue))
            {
                warningText = "\n\n⚠️ <b>Увага:</b> Для отримання сповіщень спочатку встановіть вашу групу та провайдера у меню /menu → Моя група.";
            }

            return ($"⚙️ <b>Сповіщення з каналів</b>\n\n" +
                    $"Статус: <b>{statusText}</b>\n\n" +
                    descriptionText + warningText,
                    _keyboardService.GetSettingsKeyboard(enableScraping));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Помилка при зміні налаштувань користувача {UserId}", callbackQuery.From.Id);
            return ("❌ Помилка при зміні налаштувань. Спробуйте пізніше.", _keyboardService.GetMainMenu());
        }
    }
}
