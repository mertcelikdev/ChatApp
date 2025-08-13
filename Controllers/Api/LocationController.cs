using Microsoft.AspNetCore.Mvc;
using ChatApp.Services;

namespace ChatApp.Controllers.Api;

[ApiController]
[Route("api/[controller]")]
public class LocationController : ControllerBase
{
    private readonly ILocationService _locationService;

    public LocationController(ILocationService locationService)
    {
        _locationService = locationService;
    }

    /// <summary>
    /// Tüm ülkeleri getir
    /// </summary>
    [HttpGet("countries")]
    public async Task<IActionResult> GetCountries()
    {
        try
        {
            var countries = await _locationService.GetAllCountriesAsync();
            var countryList = countries.Select(c => new { 
                id = c.Id, 
                name = c.Name
            }).ToList();

            return Ok(new { success = true, countries = countryList });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = "Ülkeler yüklenemedi: " + ex.Message });
        }
    }

    /// <summary>
    /// Belirli ülkeye ait şehirleri getir
    /// </summary>
    [HttpGet("countries/{countryId}/cities")]
    public async Task<IActionResult> GetCitiesByCountry(int countryId)
    {
        try
        {
            var cities = await _locationService.GetCitiesByCountryAsync(countryId);
            var cityList = cities.Select(c => new { 
                id = c.Id, 
                name = c.Name,
                countryName = c.Country.Name
            }).ToList();

            return Ok(new { success = true, cities = cityList });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = "Şehirler yüklenemedi: " + ex.Message });
        }
    }

    /// <summary>
    /// Konum validasyonu yap
    /// </summary>
    [HttpPost("validate")]
    public async Task<IActionResult> ValidateLocation([FromBody] ValidateLocationRequest request)
    {
        try
        {
            var country = await _locationService.GetCountryByNameAsync(request.CountryName);
            if (country == null)
            {
                return Ok(new { success = false, message = "Geçersiz ülke adı" });
            }

            var city = await _locationService.GetCityByNameAsync(request.CityName, country.Id);
            if (city == null)
            {
                return Ok(new { success = false, message = "Geçersiz şehir adı" });
            }

            var location = $"{request.CityName}, {country.Name}";
            return Ok(new { 
                success = true, 
                message = "Geçerli konum",
                location = location,
                countryId = country.Id,
                countryName = country.Name,
                cityId = city.Id,
                cityName = city.Name
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = "Validasyon hatası: " + ex.Message });
        }
    }

    /// <summary>
    /// Veritabanı istatistiklerini getir (debug için)
    /// </summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        try
        {
            var countryCount = await _locationService.GetCountryCountAsync();
            var cityCount = await _locationService.GetCityCountAsync();
            
            return Ok(new { 
                success = true, 
                countryCount = countryCount,
                cityCount = cityCount,
                message = $"{countryCount} ülke ve {cityCount} şehir bulundu"
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = "İstatistik hatası: " + ex.Message });
        }
    }
}

public class ValidateLocationRequest
{
    public string CountryName { get; set; } = string.Empty;
    public string CityName { get; set; } = string.Empty;
}
