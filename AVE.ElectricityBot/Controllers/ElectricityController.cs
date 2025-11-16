using AVE.ElectricityBot.Configuration;
using AVE.ElectricityBot.YasnoApi;
using AVE.ElectricityBot.YasnoApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace AVE.ElectricityBot.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ElectricityController : ControllerBase
{
    private readonly ILogger<ElectricityController> _logger;
    private readonly IYasnoApiClient _yasnoApiClient;
    private readonly YasnoAddressConfiguration _addressConfig;

    public ElectricityController(
        ILogger<ElectricityController> logger,
        IYasnoApiClient yasnoApiClient,
        IOptions<YasnoAddressConfiguration> addressConfig)
    {
        _logger = logger;
        _yasnoApiClient = yasnoApiClient;
        _addressConfig = addressConfig.Value;
    }

    /// <summary>
    /// Отримує інформацію про групу відключень для налаштованої адреси
    /// </summary>
    /// <returns>Інформація про групу та підгрупу</returns>
    [HttpGet("group")]
    public async Task<ActionResult<YasnoGroupResponse>> GetShutdownGroup()
    {
        try
        {
            _logger.LogInformation("Запит на отримання інформації про групу відключень");

            var result = await _yasnoApiClient.GetShutdownGroupAsync(
                _addressConfig.RegionId,
                _addressConfig.CityId,
                _addressConfig.StreetId,
                _addressConfig.HouseId,
                _addressConfig.DsoId);

            if (result == null)
            {
                _logger.LogWarning("Не вдалося отримати інформацію про групу відключень");
                return NotFound("Не вдалося отримати інформацію про групу відключень");
            }

            _logger.LogInformation("Отримано групу: {Group}, підгрупу: {Subgroup}", 
                result.Group, result.Subgroup);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Помилка при отриманні інформації про групу відключень");
            return StatusCode(500, "Помилка при отриманні даних з Yasno API");
        }
    }

    /// <summary>
    /// Отримує інформацію про групу відключень для довільної адреси
    /// </summary>
    [HttpGet("group/custom")]
    public async Task<ActionResult<YasnoGroupResponse>> GetShutdownGroupCustom(
        [FromQuery] int regionId,
        [FromQuery] int cityId,
        [FromQuery] int streetId,
        [FromQuery] int houseId,
        [FromQuery] int dsoId)
    {
        try
        {
            _logger.LogInformation("Запит на отримання інформації для довільної адреси");

            var result = await _yasnoApiClient.GetShutdownGroupAsync(
                regionId, cityId, streetId, houseId, dsoId);

            if (result == null)
            {
                return NotFound("Не вдалося отримати інформацію про групу відключень");
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Помилка при отриманні інформації про групу відключень");
            return StatusCode(500, "Помилка при отриманні даних з Yasno API");
        }
    }

    /// <summary>
    /// Отримує планові відключення для всіх груп регіону
    /// </summary>
    /// <returns>Розклад відключень</returns>
    [HttpGet("outages/planned")]
    public async Task<ActionResult<PlannedOutagesResponse>> GetPlannedOutages()
    {
        try
        {
            _logger.LogInformation("Запит на отримання планових відключень для регіону {RegionId}, DSO {DsoId}", 
                _addressConfig.RegionId, _addressConfig.DsoId);

            var result = await _yasnoApiClient.GetPlannedOutagesAsync(
                _addressConfig.RegionId,
                _addressConfig.DsoId);

            if (result == null)
            {
                _logger.LogWarning("Не вдалося отримати планові відключення");
                return NotFound("Не вдалося отримати планові відключення");
            }

            _logger.LogInformation("Отримано розклад для {Count} груп", result.Groups.Count);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Помилка при отриманні планових відключень");
            return StatusCode(500, "Помилка при отриманні даних з Yasno API");
        }
    }

    /// <summary>
    /// Отримує планові відключення для конкретної групи
    /// </summary>
    [HttpGet("outages/planned/{group}/{subgroup}")]
    public async Task<ActionResult<GroupOutageSchedule>> GetPlannedOutagesForGroup(int group, int subgroup)
    {
        try
        {
            _logger.LogInformation("Запит на отримання планових відключень для групи {Group}.{Subgroup}", 
                group, subgroup);

            var result = await _yasnoApiClient.GetPlannedOutagesAsync(
                _addressConfig.RegionId,
                _addressConfig.DsoId);

            if (result == null)
            {
                return NotFound("Не вдалося отримати планові відключення");
            }

            var schedule = result.GetSchedule(group, subgroup);
            if (schedule == null)
            {
                return NotFound($"Розклад для групи {group}.{subgroup} не знайдено");
            }

            return Ok(schedule);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Помилка при отриманні планових відключень");
            return StatusCode(500, "Помилка при отриманні даних з Yasno API");
        }
    }

    /// <summary>
    /// Отримує планові відключення для довільного регіону та DSO
    /// </summary>
    [HttpGet("outages/planned/custom")]
    public async Task<ActionResult<PlannedOutagesResponse>> GetPlannedOutagesCustom(
        [FromQuery] int regionId,
        [FromQuery] int dsoId)
    {
        try
        {
            _logger.LogInformation("Запит на отримання планових відключень для регіону {RegionId}, DSO {DsoId}", 
                regionId, dsoId);

            var result = await _yasnoApiClient.GetPlannedOutagesAsync(regionId, dsoId);

            if (result == null)
            {
                return NotFound("Не вдалося отримати планові відключення");
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Помилка при отриманні планових відключень");
            return StatusCode(500, "Помилка при отриманні даних з Yasno API");
        }
    }
}
