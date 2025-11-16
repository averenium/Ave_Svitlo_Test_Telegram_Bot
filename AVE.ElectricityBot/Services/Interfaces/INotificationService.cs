namespace AVE.ElectricityBot.Services.Interfaces;

/// <summary>
/// Сервіс для відправки автоматичних сповіщень
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Перевірити зміни та відправити повідомлення якщо потрібно
    /// </summary>
    /// <param name="forceStartup">Примусово відправити повідомлення при запуску</param>
    /// <param name="cancellationToken">Токен скасування</param>
    Task CheckAndSendNotificationAsync(bool forceStartup, CancellationToken cancellationToken);
}
