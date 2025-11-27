using AVE.ElectricityBot.Configuration;
using AVE.ElectricityBot.Services.Interfaces;
using AVE.ElectricityBot.YasnoApi;
using AVE.ElectricityBot.YasnoApi.Models;
using Microsoft.Extensions.Options;

namespace AVE.ElectricityBot.Services.Implementations;

/// <summary>
/// Сервіс для кешування розкладів відключень (оновлення раз на 5 хвилин)
/// </summary>
public class ScheduleCacheService : IScheduleCacheService
{
    private readonly IYasnoApiClient _yasnoApiClient;
    private readonly YasnoAddressConfiguration _addressConfig;
    private readonly ILogger<ScheduleCacheService> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);

    // Кеш для кожного DSO окремо
    private readonly Dictionary<int, PlannedOutagesResponse?> _cachedOutagesByDso = new();
    private readonly Dictionary<int, DateTime?> _lastUpdateByDso = new();
    private readonly Dictionary<int, DateTime?> _lastFailedAttemptByDso = new();

    // Інтервал оновлення кешу
    private readonly TimeSpan _cacheLifetime = TimeSpan.FromMinutes(5);
    private readonly TimeSpan _retryAfterFailure = TimeSpan.FromMinutes(1);

    public ScheduleCacheService(
        IYasnoApiClient yasnoApiClient,
        IOptions<YasnoAddressConfiguration> addressConfig,
        ILogger<ScheduleCacheService> logger)
    {
        _yasnoApiClient = yasnoApiClient;
        _addressConfig = addressConfig.Value;
        _logger = logger;
    }

    public async Task<PlannedOutagesResponse?> GetPlannedOutagesAsync(CancellationToken cancellationToken = default)
    {
        // За замовчуванням використовуємо DSO з конфігу
        var dsoId = _addressConfig.DsoId;
        return await GetPlannedOutagesAsync(dsoId, cancellationToken);
    }

    public async Task<PlannedOutagesResponse?> GetPlannedOutagesAsync(int dsoId, CancellationToken cancellationToken = default)
    {
        // Перевіряємо чи є валідний кеш
        if (IsCacheValid(dsoId))
        {
            _logger.LogDebug("📦 Використовую кешаний розклад для DSO={DsoId} (останнє оновлення: {LastUpdate})", 
                dsoId, _lastUpdateByDso[dsoId]!.Value.ToLocalTime());
            return _cachedOutagesByDso[dsoId];
        }

        // Перевіряємо чи не було недавньої невдалої спроби
        if (IsRetryTooSoon(dsoId))
        {
            var nextRetry = _lastFailedAttemptByDso[dsoId]!.Value.Add(_retryAfterFailure);
            var timeUntilRetry = nextRetry - DateTime.Now;
            
            _logger.LogWarning(
                "⏳ API недоступний для DSO={DsoId}. Наступна спроба через {Seconds} сек (о {NextRetry})",
                dsoId,
                (int)timeUntilRetry.TotalSeconds,
                nextRetry.ToLocalTime());

            // Повертаємо кешовані дані (навіть якщо застарілі) або null
            return _cachedOutagesByDso.GetValueOrDefault(dsoId);
        }

        // Оновлюємо кеш (з блокуванням для безпеки потоків)
        await _lock.WaitAsync(cancellationToken);
        try
        {
            // Подвійна перевірка після отримання блокування
            if (IsCacheValid(dsoId))
            {
                return _cachedOutagesByDso[dsoId];
            }

            // Перевірка на недавню невдалу спробу (подвійна перевірка)
            if (IsRetryTooSoon(dsoId))
            {
                return _cachedOutagesByDso.GetValueOrDefault(dsoId);
            }

            return await FetchAndCacheOutagesAsync(dsoId, cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task RefreshCacheAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("🔄 Примусове оновлення кешу...");
        
        // Очищаємо тільки timestamps, але зберігаємо дані
        _lastUpdateByDso.Clear();
        _lastFailedAttemptByDso.Clear();
        
        // Оновлюємо кеш для обох DSO
        await GetPlannedOutagesAsync(301, cancellationToken); // ДТЕК
        await GetPlannedOutagesAsync(303, cancellationToken); // ЦЕК
    }

    public void ClearCache()
    {
        _cachedOutagesByDso.Clear();
        _lastUpdateByDso.Clear();
        _lastFailedAttemptByDso.Clear();
        _logger.LogInformation("🗑️ Кеш очищено");
    }

    /// <summary>
    /// Перевіряє чи є валідний кеш для DSO
    /// </summary>
    private bool IsCacheValid(int dsoId)
    {
        return _cachedOutagesByDso.ContainsKey(dsoId) &&
               _lastUpdateByDso.TryGetValue(dsoId, out var lastUpdate) &&
               lastUpdate.HasValue &&
               DateTime.Now.Date == lastUpdate.Value.Date &&
               DateTime.Now - lastUpdate.Value < _cacheLifetime;
    }

    /// <summary>
    /// Перевіряє чи занадто рано для повторної спроби після помилки
    /// </summary>
    private bool IsRetryTooSoon(int dsoId)
    {
        return _lastFailedAttemptByDso.TryGetValue(dsoId, out var lastFailedAttempt) &&
               lastFailedAttempt.HasValue &&
               DateTime.Now - lastFailedAttempt.Value < _retryAfterFailure;
    }

    /// <summary>
    /// Отримує дані з API та оновлює кеш
    /// </summary>
    private async Task<PlannedOutagesResponse?> FetchAndCacheOutagesAsync(int dsoId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("🔄 Оновлення кешу розкладу (regionId={RegionId}, dsoId={DsoId})...", 
            _addressConfig.RegionId, dsoId);

        try
        {
            var outages = await _yasnoApiClient.GetPlannedOutagesAsync(
                _addressConfig.RegionId,
                dsoId,
                cancellationToken);

            if (outages != null)
            {
                UpdateCacheOnSuccess(dsoId, outages);
            }
            else
            {
                UpdateCacheOnFailure(dsoId);
            }

            return outages;
        }
        catch (Exception ex)
        {
            UpdateCacheOnError(dsoId, ex);
            // Повертаємо кешовані дані (навіть якщо застарілі) або null
            return _cachedOutagesByDso.GetValueOrDefault(dsoId);
        }
    }

    /// <summary>
    /// Оновлює кеш при успішному отриманні даних
    /// </summary>
    private void UpdateCacheOnSuccess(int dsoId, PlannedOutagesResponse outages)
    {
        _cachedOutagesByDso[dsoId] = outages;
        _lastUpdateByDso[dsoId] = DateTime.Now;
        
        // Скидаємо час невдалої спроби при успіху
        _lastFailedAttemptByDso.Remove(dsoId);

        _logger.LogInformation("✅ Кеш оновлено для DSO={DsoId}. Груп: {Count}. Наступне оновлення: {NextUpdate}", 
            dsoId,
            outages.Groups.Count,
            _lastUpdateByDso[dsoId]!.Value.Add(_cacheLifetime).ToLocalTime());
    }

    /// <summary>
    /// Оновлює стан при невдалому отриманні даних (API повернув null)
    /// </summary>
    private void UpdateCacheOnFailure(int dsoId)
    {
        // Запам'ятовуємо час невдалої спроби
        _lastFailedAttemptByDso[dsoId] = DateTime.Now;
        _logger.LogWarning(
            "❌ Не вдалося отримати розклад з API для DSO={DsoId}. Наступна спроба о {NextRetry}",
            dsoId,
            DateTime.Now.Add(_retryAfterFailure).ToLocalTime());
    }

    /// <summary>
    /// Оновлює стан при помилці запиту до API
    /// </summary>
    private void UpdateCacheOnError(int dsoId, Exception ex)
    {
        // Запам'ятовуємо час невдалої спроби при винятку
        _lastFailedAttemptByDso[dsoId] = DateTime.Now;

        _logger.LogError(ex, "❌ Помилка при запиті до API для DSO={DsoId}. Наступна спроба о {NextRetry}",
            dsoId,
            DateTime.Now.Add(_retryAfterFailure).ToLocalTime());
    }
}
