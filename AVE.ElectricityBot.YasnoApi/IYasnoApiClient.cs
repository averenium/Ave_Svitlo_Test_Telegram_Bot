using AVE.ElectricityBot.YasnoApi.Models;

namespace AVE.ElectricityBot.YasnoApi;

/// <summary>
/// Інтерфейс для роботи з API Yasno
/// </summary>
public interface IYasnoApiClient
{
    /// <summary>
    /// Отримує інформацію про групу відключень для вказаної адреси
    /// </summary>
    /// <param name="request">Параметри адреси</param>
    /// <param name="cancellationToken">Токен скасування</param>
    /// <returns>Інформація про групу та підгрупу</returns>
    Task<YasnoGroupResponse?> GetShutdownGroupAsync(
        YasnoAddressRequest request,
        CancellationToken cancellationToken = default);

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
    Task<YasnoGroupResponse?> GetShutdownGroupAsync(
        int regionId,
        int cityId,
        int streetId,
        int houseId,
        int dsoId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Отримує планові відключення для всіх груп регіону
    /// </summary>
    /// <param name="regionId">ID регіону</param>
    /// <param name="dsoId">ID оператора DSO</param>
    /// <param name="cancellationToken">Токен скасування</param>
    /// <returns>Розклад відключень для всіх груп</returns>
    Task<PlannedOutagesResponse?> GetPlannedOutagesAsync(
        int regionId,
        int dsoId,
        CancellationToken cancellationToken = default);
}
