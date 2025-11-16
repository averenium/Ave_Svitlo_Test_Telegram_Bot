using Telegram.Bot.Types.ReplyMarkups;

namespace AVE.ElectricityBot.Services.Interfaces;

/// <summary>
/// Сервіс для створення клавіатур Telegram
/// </summary>
public interface IKeyboardService
{
    /// <summary>
    /// Отримати головне меню бота
    /// </summary>
    InlineKeyboardMarkup GetMainMenu();

    /// <summary>
    /// Отримати меню вибору груп для команди /other
    /// </summary>
    InlineKeyboardMarkup GetGroupSelectionMenu();

    /// <summary>
    /// Отримати меню повернення до головного
    /// </summary>
    InlineKeyboardMarkup GetBackToMainMenu();

    /// <summary>
    /// Отримати меню з опціями для розкладу
    /// </summary>
    InlineKeyboardMarkup GetScheduleMenu();
    InlineKeyboardMarkup GetMyGroupSelectionMenu();
    InlineKeyboardMarkup GetDsoSelectionMenu();
    InlineKeyboardMarkup GetDsoSelectionMenuForOther();
    InlineKeyboardMarkup GetGroupSelectionMenuForDso(int dsoId);
    InlineKeyboardMarkup GetGroupSelectionMenuForOther(int dsoId);
    InlineKeyboardMarkup GetMyGroupMenuWithChange();
    InlineKeyboardMarkup GetSettingsKeyboard(bool channelScrapingEnabled);
}
