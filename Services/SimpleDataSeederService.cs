using System.Text.Json;
using ChatApp.Data;
using ChatApp.Models;
using Microsoft.EntityFrameworkCore;

namespace ChatApp.Services;

public interface ISimpleDataSeederService
{
    Task SeedCountriesAndCitiesAsync();
    Task SeedUsersAsync();
}

public class SimpleDataSeederService : ISimpleDataSeederService
{
    private readonly ChatDbContext _context;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<SimpleDataSeederService> _logger;
    private readonly IEncryptionService _encryptionService;

    public SimpleDataSeederService(ChatDbContext context, IWebHostEnvironment env, ILogger<SimpleDataSeederService> logger, IEncryptionService encryptionService)
    {
        _context = context;
        _env = env;
        _logger = logger;
        _encryptionService = encryptionService;
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

            _logger.LogInformation("🚀 Starting to seed countries and cities from countries.json...");

            var countriesJsonPath = Path.Combine(_env.ContentRootPath, "Data", "countries.json");
            
            if (!File.Exists(countriesJsonPath))
            {
                _logger.LogError($"❌ Countries JSON file not found: {countriesJsonPath}");
                return;
            }

            var jsonContent = await File.ReadAllTextAsync(countriesJsonPath);
            var countriesData = JsonSerializer.Deserialize<Dictionary<string, string[]>>(jsonContent);

            if (countriesData == null)
            {
                _logger.LogError("❌ Failed to parse countries.json");
                return;
            }

            var countries = new List<Country>();
            var cities = new List<City>();

            foreach (var kvp in countriesData)
            {
                var countryName = kvp.Key;
                var cityNames = kvp.Value;

                // Ülkeyi oluştur
                var country = new Country
                {
                    Name = countryName,
                    CreatedAt = DateTime.UtcNow
                };

                countries.Add(country);
            }

            // Önce ülkeleri kaydet
            await _context.Countries.AddRangeAsync(countries);
            await _context.SaveChangesAsync();

            // Şimdi şehirleri CountryId ile kaydet
            foreach (var kvp in countriesData)
            {
                var countryName = kvp.Key;
                var cityNames = kvp.Value;

                var country = countries.FirstOrDefault(c => c.Name == countryName);
                if (country != null)
                {
                    // Her ülke için benzersiz şehir isimlerini al
                    var uniqueCityNames = cityNames.Distinct().ToArray();
                    
                    foreach (var cityName in uniqueCityNames)
                    {
                        var city = new City
                        {
                            Name = cityName,
                            CountryId = country.Id,
                            CreatedAt = DateTime.UtcNow
                        };

                        cities.Add(city);
                    }
                }
            }

            // Şehirleri batch olarak kaydet
            if (cities.Any())
            {
                await _context.Cities.AddRangeAsync(cities);
                await _context.SaveChangesAsync();
            }

            _logger.LogInformation($"✅ Successfully seeded {countries.Count} countries and {cities.Count} cities");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error occurred while seeding countries and cities");
            throw;
        }
    }

    public async Task SeedUsersAsync()
    {
        try
        {
            // Eğer zaten kullanıcı varsa, seed etme
            var existingUsersCount = await _context.Users.CountAsync();
            if (existingUsersCount > 0)
            {
                _logger.LogInformation($"👥 Users already exist in database: {existingUsersCount} users found");
                return;
            }

            _logger.LogInformation("🚀 Starting to seed demo users...");

            // Türkiye ve İstanbul'u al (varsayılan lokasyon için)
            var turkey = await _context.Countries.FirstOrDefaultAsync(c => c.Name.Contains("Turkey"));
            var istanbul = turkey != null ? await _context.Cities.FirstOrDefaultAsync(c => c.Name.Contains("Istanbul") && c.CountryId == turkey.Id) : null;

            var users = new List<User>
            {
                new User
                {
                    Username = "admin",
                    DisplayName = "Admin User",
                    Email = "admin@chatapp.com",
                    PasswordHash = _encryptionService.HashPassword("admin123"),
                    Bio = "ChatApp Yöneticisi",
                    Location = "İstanbul, Türkiye",
                    CountryId = turkey?.Id,
                    CityId = istanbul?.Id,
                    UserStatus = UserStatusOptions.Online,
                    IsOnline = true,
                    CreatedAt = DateTime.UtcNow,
                    LastActive = DateTime.UtcNow
                },
                new User
                {
                    Username = "john_doe",
                    DisplayName = "John Doe",
                    Email = "john@example.com",
                    PasswordHash = _encryptionService.HashPassword("password123"),
                    Bio = "Yazılım geliştiricisi ve teknoloji meraklısı",
                    Location = "İstanbul, Türkiye",
                    CountryId = turkey?.Id,
                    CityId = istanbul?.Id,
                    UserStatus = UserStatusOptions.Online,
                    IsOnline = false,
                    CreatedAt = DateTime.UtcNow.AddDays(-30),
                    LastActive = DateTime.UtcNow.AddHours(-2)
                },
                new User
                {
                    Username = "jane_smith",
                    DisplayName = "Jane Smith",
                    Email = "jane@example.com",
                    PasswordHash = _encryptionService.HashPassword("password123"),
                    Bio = "UI/UX Designer ve yaratıcı düşünür",
                    Location = "Ankara, Türkiye",
                    CountryId = turkey?.Id,
                    CityId = turkey != null ? await _context.Cities.FirstOrDefaultAsync(c => c.Name.Contains("Ankara") && c.CountryId == turkey.Id) != null ? (await _context.Cities.FirstOrDefaultAsync(c => c.Name.Contains("Ankara") && c.CountryId == turkey.Id))!.Id : null : null,
                    UserStatus = UserStatusOptions.Away,
                    IsOnline = false,
                    CreatedAt = DateTime.UtcNow.AddDays(-15),
                    LastActive = DateTime.UtcNow.AddMinutes(-30)
                },
                new User
                {
                    Username = "mike_wilson",
                    DisplayName = "Mike Wilson",
                    Email = "mike@example.com",
                    PasswordHash = _encryptionService.HashPassword("password123"),
                    Bio = "DevOps Engineer ve automation uzmanı",
                    Location = "İzmir, Türkiye",
                    CountryId = turkey?.Id,
                    CityId = turkey != null ? await _context.Cities.FirstOrDefaultAsync(c => c.Name.Contains("Izmir") && c.CountryId == turkey.Id) != null ? (await _context.Cities.FirstOrDefaultAsync(c => c.Name.Contains("Izmir") && c.CountryId == turkey.Id))!.Id : null : null,
                    UserStatus = UserStatusOptions.Busy,
                    IsOnline = true,
                    CreatedAt = DateTime.UtcNow.AddDays(-7),
                    LastActive = DateTime.UtcNow.AddMinutes(-10)
                },
                new User
                {
                    Username = "sarah_jones",
                    DisplayName = "Sarah Jones",
                    Email = "sarah@example.com",
                    PasswordHash = _encryptionService.HashPassword("password123"),
                    Bio = "Frontend Developer ve React uzmanı",
                    Location = "Bursa, Türkiye",
                    CountryId = turkey?.Id,
                    CityId = turkey != null ? await _context.Cities.FirstOrDefaultAsync(c => c.Name.Contains("Bursa") && c.CountryId == turkey.Id) != null ? (await _context.Cities.FirstOrDefaultAsync(c => c.Name.Contains("Bursa") && c.CountryId == turkey.Id))!.Id : null : null,
                    UserStatus = UserStatusOptions.Online,
                    IsOnline = true,
                    CreatedAt = DateTime.UtcNow.AddDays(-3),
                    LastActive = DateTime.UtcNow
                },
                new User
                {
                    Username = "demo_user",
                    DisplayName = "Demo User",
                    Email = "demo@chatapp.com",
                    PasswordHash = _encryptionService.HashPassword("demo123"),
                    Bio = "ChatApp demo kullanıcısı",
                    Location = "İstanbul, Türkiye",
                    CountryId = turkey?.Id,
                    CityId = istanbul?.Id,
                    UserStatus = UserStatusOptions.Online,
                    IsOnline = false,
                    CreatedAt = DateTime.UtcNow.AddDays(-1),
                    LastActive = DateTime.UtcNow.AddHours(-1)
                }
            };

            await _context.Users.AddRangeAsync(users);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"✅ Successfully seeded {users.Count} demo users");
            _logger.LogInformation("👤 Demo Kullanıcılar:");
            _logger.LogInformation("   - admin / admin123 (Admin)");
            _logger.LogInformation("   - john_doe / password123");
            _logger.LogInformation("   - jane_smith / password123");
            _logger.LogInformation("   - mike_wilson / password123");
            _logger.LogInformation("   - sarah_jones / password123");
            _logger.LogInformation("   - demo_user / demo123");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error occurred while seeding users");
            throw;
        }
    }
}
