using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AVE.ElectricityBot.YasnoApi.Models;

namespace AVE.ElectricityBot.YasnoApi;

/// <summary>
/// Клієнт для роботи з API Yasno
/// </summary>
public class YasnoApiClient : IYasnoApiClient, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly bool _shouldDisposeHttpClient;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>
    /// Ініціалізує новий екземпляр YasnoApiClient
    /// </summary>
    public YasnoApiClient() : this(new HttpClient())
    {
        _shouldDisposeHttpClient = true;
        _httpClient.BaseAddress = new Uri("https://app.yasno.ua/api/blackout-service/public/");
    }

    /// <summary>
    /// Ініціалізує новий екземпляр YasnoApiClient з власним HttpClient (для DI)
    /// </summary>
    /// <param name="httpClient">HTTP клієнт для запитів</param>
    public YasnoApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _shouldDisposeHttpClient = false;
        
        // Встановлюємо BaseAddress тільки якщо вона ще не встановлена
        if (_httpClient.BaseAddress == null)
        {
            _httpClient.BaseAddress = new Uri("https://app.yasno.ua/api/blackout-service/public/");
        }
    }

    /// <summary>
    /// Отримує інформацію про групу відключень для вказаної адреси
    /// </summary>
    /// <param name="request">Параметри адреси</param>
    /// <param name="cancellationToken">Токен скасування</param>
    /// <returns>Інформація про групу та підгрупу</returns>
    /// <exception cref="HttpRequestException">При помилці HTTP запиту</exception>
    /// <exception cref="JsonException">При помилці десеріалізації відповіді</exception>
    public async Task<YasnoGroupResponse?> GetShutdownGroupAsync(
        YasnoAddressRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var endpoint = $"shutdowns/addresses/v2/group?{request.ToQueryString()}";
        
        var response = await _httpClient.GetAsync(endpoint, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<YasnoGroupResponse>(JsonOptions, cancellationToken);
    }

    /// <summary>
    /// Отримує інформацію про групу відключень для вказаної адреси (спрощений метод)
    /// </summary>
    /// <param name="regionId">ID регіону</param>
    /// <param name="cityId">ID міста</param>
    /// <param name="streetId">ID вулиці</param>
    /// <param name="houseId">ID будинку</param>
    /// <param name="dsoId">ID DSO</param>
    /// <param name="cancellationToken">Токен скасування</param>
    /// <returns>Інформація про групу та підгрупу</returns>
    public async Task<YasnoGroupResponse?> GetShutdownGroupAsync(
        int regionId,
        int cityId,
        int streetId,
        int houseId,
        int dsoId,
        CancellationToken cancellationToken = default)
    {
        var request = new YasnoAddressRequest
        {
            RegionId = regionId,
            CityId = cityId,
            StreetId = streetId,
            HouseId = houseId,
            DsoId = dsoId
        };

        return await GetShutdownGroupAsync(request, cancellationToken);
    }

    /// <summary>
    /// Отримує планові відключення для всіх груп регіону
    /// </summary>
    /// <param name="regionId">ID регіону</param>
    /// <param name="dsoId">ID оператора DSO</param>
    /// <param name="cancellationToken">Токен скасування</param>
    /// <returns>Розклад відключень для всіх груп</returns>
    /// <exception cref="HttpRequestException">При помилці HTTP запиту</exception>
    /// <exception cref="JsonException">При помилці десеріалізації відповіді</exception>
    public async Task<PlannedOutagesResponse?> GetPlannedOutagesAsync(
        int regionId,
        int dsoId,
        CancellationToken cancellationToken = default)
    {
        var endpoint = $"shutdowns/regions/{regionId}/dsos/{dsoId}/planned-outages";
        
        var response = await _httpClient.GetAsync(endpoint, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<PlannedOutagesResponse>(JsonOptions, cancellationToken);
    }

    public void Dispose()
    {
        if (_shouldDisposeHttpClient)
        {
            _httpClient?.Dispose();
        }
        GC.SuppressFinalize(this);
    }
}
