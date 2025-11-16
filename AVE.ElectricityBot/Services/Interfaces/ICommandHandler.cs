using Telegram.Bot.Types;

namespace AVE.ElectricityBot.Services.Interfaces;

/// <summary>
/// Базовий інтерфейс для обробників команд
/// </summary>
public interface ICommandHandler
{
    /// <summary>
    /// Назва команди (без /)
    /// </summary>
    string CommandName { get; }

    /// <summary>
    /// Опис команди для меню Telegram
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Обробити команду
    /// </summary>
    Task<string> HandleAsync(Message message, CancellationToken cancellationToken);
}
