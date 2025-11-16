using AVE.ElectricityBot.YasnoApi.Models;

namespace AVE.ElectricityBot.Services.Interfaces;

/// <summary>
/// Сервіс для кешування розкладів відключень
/// </summary>
public interface IScheduleCacheService
{
    /// <summary>
    /// Отримати розклад для всіх груп (з кешу або API) для DSO за замовчуванням
    /// </summary>
    Task<PlannedOutagesResponse?> GetPlannedOutagesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Отримати розклад для всіх груп (з кешу або API) для конкретного DSO
    /// </summary>
    Task<PlannedOutagesResponse?> GetPlannedOutagesAsync(int dsoId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Примусово оновити кеш
    /// </summary>
    Task RefreshCacheAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Очистити кеш
    /// </summary>
    void ClearCache();
}
