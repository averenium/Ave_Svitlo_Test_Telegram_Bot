namespace AVE.ElectricityBot.YasnoApi.Models;

/// <summary>
/// Параметри запиту для отримання групи відключень
/// </summary>
public class YasnoAddressRequest
{
    /// <summary>
    /// ID регіону
    /// </summary>
    public int RegionId { get; set; }

    /// <summary>
    /// ID міста
    /// </summary>
    public int CityId { get; set; }

    /// <summary>
    /// ID вулиці
    /// </summary>
    public int StreetId { get; set; }

    /// <summary>
    /// ID будинку
    /// </summary>
    public int HouseId { get; set; }

    /// <summary>
    /// ID оператора системи розподілу (DSO)
    /// </summary>
    public int DsoId { get; set; }

    /// <summary>
    /// Конвертує параметри в query string
    /// </summary>
    public string ToQueryString()
    {
        return $"regionId={RegionId}&cityId={CityId}&streetId={StreetId}&houseId={HouseId}&dsoId={DsoId}";
    }
}
