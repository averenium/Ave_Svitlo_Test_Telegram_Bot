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

    // Інтервал оновлення кешу
    private readonly TimeSpan _cacheLifetime = TimeSpan.FromMinutes(5);

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
        // Перевіряємо чи потрібно оновити кеш
        if (_cachedOutagesByDso.ContainsKey(dsoId) &&
            _lastUpdateByDso.ContainsKey(dsoId) &&
            _lastUpdateByDso[dsoId].HasValue &&
            DateTime.Now - _lastUpdateByDso[dsoId]!.Value < _cacheLifetime)
        {
            _logger.LogDebug("?? Використовую кешаний розклад для DSO={DsoId} (останнє оновлення: {LastUpdate})", 
                dsoId, _lastUpdateByDso[dsoId]!.Value.ToLocalTime());
            return _cachedOutagesByDso[dsoId];
        }

        // Оновлюємо кеш (з блокуванням для безпеки потоків)
        await _lock.WaitAsync(cancellationToken);
        try
        {
            // Подвійна перевірка після отримання блокування
            if (_cachedOutagesByDso.ContainsKey(dsoId) &&
                _lastUpdateByDso.ContainsKey(dsoId) &&
                _lastUpdateByDso[dsoId].HasValue &&
                DateTime.Now - _lastUpdateByDso[dsoId]!.Value < _cacheLifetime)
            {
                return _cachedOutagesByDso[dsoId];
            }

            _logger.LogInformation("?? Оновлення кешу розкладу (regionId={RegionId}, dsoId={DsoId})...", 
                _addressConfig.RegionId, dsoId);

            var outages = await _yasnoApiClient.GetPlannedOutagesAsync(
                _addressConfig.RegionId,
                dsoId,
                cancellationToken);

            if (outages != null)
            {
                _cachedOutagesByDso[dsoId] = outages;
                _lastUpdateByDso[dsoId] = DateTime.Now;

                _logger.LogInformation("? Кеш оновлено для DSO={DsoId}. Груп: {Count}. Наступне оновлення: {NextUpdate}", 
                    dsoId,
                    outages.Groups.Count,
                    _lastUpdateByDso[dsoId]!.Value.Add(_cacheLifetime).ToLocalTime());
            }
            else
            {
                _logger.LogWarning("?? Не вдалося отримати розклад з API для DSO={DsoId}", dsoId);
            }

            return outages;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task RefreshCacheAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("?? Примусове оновлення кешу...");
        
        ClearCache();
        
        // Оновлюємо кеш для обох DSO
        await GetPlannedOutagesAsync(301, cancellationToken); // ДТЕК
        await GetPlannedOutagesAsync(303, cancellationToken); // ЦЕК
    }

    public void ClearCache()
    {
        _cachedOutagesByDso.Clear();
        _lastUpdateByDso.Clear();
        _logger.LogInformation("??? Кеш очищено");
    }
}
