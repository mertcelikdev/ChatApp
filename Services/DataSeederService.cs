using System.Text.Json;
using ChatApp.Data;
using ChatApp.Models;
using Microsoft.EntityFrameworkCore;

namespace ChatApp.Services;

public interface IDataSeederService
{
    Task SeedCountriesAndCitiesAsync();
}

public class DataSeederService : IDataSeederService
{
    private readonly ChatDbContext _context;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<DataSeederService> _logger;

    public DataSeederService(ChatDbContext context, IWebHostEnvironment env, ILogger<DataSeederService> logger)
    {
        _context = context;
        _env = env;
        _logger = logger;
    }

    public async Task SeedCountriesAndCitiesAsync()
    {
        try
        {
            // Eğer zaten veri varsa, seed etme
            var existingCountryCount = await _context.Countries.CountAsync();
            if (existingCountryCount > 0)
            {
                _logger.LogInformation($"🌍 Countries already exist in database: {existingCountryCount} countries found");
                return;
            }

            _logger.LogInformation("🚀 Starting to seed countries and cities from JSON files...");

            var citiesSplitPath = Path.Combine(_env.ContentRootPath, "Data", "cities_split");
            
            if (!Directory.Exists(citiesSplitPath))
            {
                _logger.LogError($"❌ Cities split directory not found: {citiesSplitPath}");
                return;
            }

            var jsonFiles = Directory.GetFiles(citiesSplitPath, "*.json")
                .Where(f => !Path.GetFileName(f).Equals("countries.json", StringComparison.OrdinalIgnoreCase) 
                        && !Path.GetFileName(f).Equals("index.json", StringComparison.OrdinalIgnoreCase))
                .OrderBy(f => f)
                .ToList();

            _logger.LogInformation($"📁 Found {jsonFiles.Count} country files to process");

            var countryBatch = new List<Country>();
            var cityBatch = new List<City>();
            int totalCities = 0;

            foreach (var jsonFile in jsonFiles)
            {
                try
                {
                    var fileName = Path.GetFileNameWithoutExtension(jsonFile);
                    var countryName = fileName.Replace("_", " ").Replace("  ", " ");

                    // Uzunluk kontrolü
                    if (fileName.Length > 50)
                    {
                        _logger.LogWarning($"⚠️ Country code too long, skipping: {fileName} (length: {fileName.Length})");
                        continue;
                    }

                    if (countryName.Length > 100)
                    {
                        _logger.LogWarning($"⚠️ Country name too long, skipping: {countryName} (length: {countryName.Length})");
                        continue;
                    }

                    // Country oluştur
                    var country = new Country
                    {
                        Name = countryName
                    };
                    countryBatch.Add(country);

                    // JSON'dan şehirleri oku
                    var jsonContent = await File.ReadAllTextAsync(jsonFile);
                    var cities = JsonSerializer.Deserialize<List<string>>(jsonContent);

                    if (cities != null && cities.Any())
                    {
                        _logger.LogInformation($"🏙️ Processing {countryName}: {cities.Count} cities");

                        foreach (var cityName in cities.Where(c => !string.IsNullOrWhiteSpace(c)).Distinct())
                        {
                            var cleanCityName = cityName.Trim();
                            if (cleanCityName.Length > 100)
                            {
                                _logger.LogWarning($"⚠️ City name too long, skipping: {cleanCityName} (length: {cleanCityName.Length})");
                                continue;
                            }

                            cityBatch.Add(new City
                            {
                                Name = cleanCityName,
                                Country = country
                            });
                            totalCities++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"❌ Error processing file {jsonFile}: {ex.Message}");
                    continue;
                }

                // Her 10 ülkede bir batch insert yap (memory tasarrufu için)
                if (countryBatch.Count >= 10)
                {
                    await SaveBatchAsync(countryBatch, cityBatch);
                    countryBatch.Clear();
                    cityBatch.Clear();
                    
                    _logger.LogInformation($"✅ Saved batch, total cities processed: {totalCities}");
                }
            }

            // Kalan veriyi kaydet
            if (countryBatch.Any())
            {
                await SaveBatchAsync(countryBatch, cityBatch);
            }

            var finalCountryCount = await _context.Countries.CountAsync();
            var finalCityCount = await _context.Cities.CountAsync();

            _logger.LogInformation($"🎉 Data seeding completed successfully!");
            _logger.LogInformation($"📊 Final counts: {finalCountryCount} countries, {finalCityCount} cities");
        }
        catch (Exception ex)
        {
            _logger.LogError($"❌ Data seeding failed: {ex.Message}");
            _logger.LogError($"Stack trace: {ex.StackTrace}");
            throw;
        }
    }

    private async Task SaveBatchAsync(List<Country> countries, List<City> cities)
    {
        try
        {
            _logger.LogInformation($"💾 Saving batch: {countries.Count} countries, {cities.Count} cities");
            
            using var transaction = await _context.Database.BeginTransactionAsync();
            
            // Önce countries'i kaydet
            await _context.Countries.AddRangeAsync(countries);
            await _context.SaveChangesAsync();
            
            _logger.LogInformation($"✅ Countries saved successfully");
            
            // Sonra cities'i kaydet (foreign key referansları kuruldu)
            await _context.Cities.AddRangeAsync(cities);
            await _context.SaveChangesAsync();
            
            _logger.LogInformation($"✅ Cities saved successfully");
            
            await transaction.CommitAsync();
            _logger.LogInformation($"✅ Transaction committed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError($"❌ Batch save failed: {ex.Message}");
            _logger.LogError($"Inner exception: {ex.InnerException?.Message}");
            _logger.LogError($"Stack trace: {ex.StackTrace}");
            throw;
        }
    }
}
