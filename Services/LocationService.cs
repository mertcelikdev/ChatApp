using System.Text.Json;
using ChatApp.Models;
using ChatApp.Data;
using Microsoft.EntityFrameworkCore;

namespace ChatApp.Services;

public interface ILocationService
{
    Task<List<Country>> GetAllCountriesAsync();
    Task<List<City>> GetCitiesByCountryAsync(int countryId);
    Task<Country?> GetCountryByNameAsync(string countryName);
    Task<City?> GetCityByNameAsync(string cityName, int countryId);
    Task<LocationDto?> ParseLocationAsync(string location);
    Task<int> GetCountryCountAsync();
    Task<int> GetCityCountAsync();
}

public class LocationService : ILocationService
{
    private readonly ChatDbContext _context;

    public LocationService(ChatDbContext context)
    {
        _context = context;
    }

    public async Task<List<Country>> GetAllCountriesAsync()
    {
        return await _context.Countries
            .OrderBy(c => c.Name)
            .ToListAsync();
    }

    public async Task<List<City>> GetCitiesByCountryAsync(int countryId)
    {
        return await _context.Cities
            .Include(c => c.Country)
            .Where(c => c.CountryId == countryId)
            .OrderBy(c => c.Name)
            .ToListAsync();
    }

    public async Task<Country?> GetCountryByNameAsync(string countryName)
    {
        return await _context.Countries
            .FirstOrDefaultAsync(c => c.Name.Equals(countryName, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<City?> GetCityByNameAsync(string cityName, int countryId)
    {
        return await _context.Cities
            .Include(c => c.Country)
            .FirstOrDefaultAsync(c => c.Name.Equals(cityName, StringComparison.OrdinalIgnoreCase) 
                                  && c.CountryId == countryId);
    }

    public async Task<LocationDto?> ParseLocationAsync(string location)
    {
        if (string.IsNullOrWhiteSpace(location)) return null;
        
        var parts = location.Split(',').Select(p => p.Trim()).ToArray();
        if (parts.Length >= 2)
        {
            var cityName = parts[0];
            var countryName = parts[1];
            
            var country = await GetCountryByNameAsync(countryName);
            if (country != null)
            {
                var city = await GetCityByNameAsync(cityName, country.Id);
                if (city != null)
                {
                    return new LocationDto 
                    { 
                        CityId = city.Id, 
                        CityName = city.Name, 
                        CountryName = country.Name 
                    };
                }
            }
        }
        return null;
    }

    public async Task<int> GetCountryCountAsync()
    {
        return await _context.Countries.CountAsync();
    }

    public async Task<int> GetCityCountAsync()
    {
        return await _context.Cities.CountAsync();
    }
}
