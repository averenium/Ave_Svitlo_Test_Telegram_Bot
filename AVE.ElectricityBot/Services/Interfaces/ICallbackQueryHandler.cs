using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace AVE.ElectricityBot.Services.Interfaces;

/// <summary>
/// Сервіс для обробки callback-запитів від інлайн-кнопок
/// </summary>
public interface ICallbackQueryHandler
{
    /// <summary>
    /// Обробити callback-запит
    /// </summary>
    Task<(string response, InlineKeyboardMarkup? keyboard)> HandleAsync(
        CallbackQuery callbackQuery, 
        CancellationToken cancellationToken);
}
