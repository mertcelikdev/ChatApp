using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ChatApp.Services;
using ChatApp.Models;
using Microsoft.EntityFrameworkCore;
using ChatApp.Data;
using System.Security.Claims;
using System.Text.Json;

namespace ChatApp.Controllers;

[Authorize]
public class ProfileController : Controller
{
    private readonly IUserService _userService;
    private readonly IS3Service _s3Service;
    private readonly ChatDbContext _context;
    private readonly IWebHostEnvironment _env;

    public ProfileController(IUserService userService, IS3Service s3Service, ChatDbContext context, IWebHostEnvironment env)
    {
        _userService = userService;
        _s3Service = s3Service;
        _context = context;
        _env = env;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        var username = User.Identity?.Name ?? "";
        
        if (userId == 0)
        {
            return RedirectToAction("Login", "Auth");
        }

        var user = await _userService.GetUserByUsernameAsync(username);
        if (user == null)
        {
            return RedirectToAction("Index", "Home");
        }

        // Profil fotoğrafı yoksa default URL set et
        if (string.IsNullOrEmpty(user.ProfileImageUrl))
        {
            user.ProfileImageUrl = _s3Service.GetDefaultAvatarUrl();
        }

        // Navbar için profil bilgisini ekle
        ViewBag.ProfileImageUrl = user.ProfileImageUrl;
        ViewBag.Title = "Profil Ayarları";
        return View(user);
    }

    [HttpPost]
    public async Task<IActionResult> UpdateProfile(User model, IFormFile? profileImage)
    {
        // Claims'den kullanıcı bilgilerini al
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        var username = User.Identity?.Name ?? "";
        
        if (userId == 0)
        {
            Console.WriteLine("❌ UpdateProfile: Invalid user session");
            return Json(new { success = false, message = "Oturum süreniz dolmuş. Lütfen tekrar giriş yapın." });
        }

        try
        {
            Console.WriteLine($"🔄 UpdateProfile started for user: {username} (ID: {userId})");
            Console.WriteLine($"📝 Form data: DisplayName={model.DisplayName}, Email={model.Email}, Bio={model.Bio}, Location={model.Location}, CountryId={model.CountryId}, CityId={model.CityId}, UserStatus={model.UserStatus}");
            
            var user = await _userService.GetUserByUsernameAsync(username);
            if (user == null)
            {
                Console.WriteLine($"❌ User not found: {username}");
                return Json(new { success = false, message = "Kullanıcı bulunamadı" });
            }

            // Profil bilgilerini güncelle
            user.DisplayName = model.DisplayName;
            user.Email = model.Email;
            user.Bio = model.Bio;
            user.Location = model.Location;
            user.CountryId = model.CountryId;
            user.CityId = model.CityId;
            user.UserStatus = NormalizeStatus(model.UserStatus) ?? user.UserStatus;

            Console.WriteLine($"📝 Profile data updated for user: {username}");

            // Profil fotoğrafı yükleme
            if (profileImage != null && profileImage.Length > 0)
            {
                try
                {
                    Console.WriteLine($"📸 Uploading profile image for user: {username}");
                    
                    // Eski profil fotoğrafını sil
                    if (!string.IsNullOrEmpty(user.ProfileImageUrl) && 
                        !user.ProfileImageUrl.Contains("ui-avatars.com"))
                    {
                        await _s3Service.DeleteImageAsync(user.ProfileImageUrl);
                        Console.WriteLine($"🗑️ Old profile image deleted for user: {username}");
                    }

                    // Yeni profil fotoğrafını yükle
                    var imageUrl = await _s3Service.UploadProfileImageAsync(profileImage, user.Id);
                    user.ProfileImageUrl = imageUrl;
                    Console.WriteLine($"✅ New profile image uploaded: {imageUrl}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Profile image upload failed: {ex.Message}");
                    return Json(new { success = false, message = $"Profil fotoğrafı yüklenemedi: {ex.Message}" });
                }
            }

            // Veritabanını güncelle
            await _userService.UpdateUserAsync(user);
            Console.WriteLine($"✅ Profile updated successfully for user: {username}");

            return Json(new { 
                success = true, 
                message = "Profil başarıyla güncellendi",
                profileImageUrl = user.ProfileImageUrl ?? _s3Service.GetDefaultAvatarUrl()
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ UpdateProfile error: {ex.Message}");
            return Json(new { success = false, message = $"Profil güncellenemedi: {ex.Message}" });
        }
    }

    [HttpPost]
    public async Task<IActionResult> UploadProfileImage(IFormFile profileImage)
    {
        // Claims'den kullanıcı bilgilerini al
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        var username = User.Identity?.Name ?? "";
        
        if (userId == 0)
        {
            Console.WriteLine("❌ UploadProfileImage: Invalid user session");
            return Json(new { success = false, message = "Oturum süreniz dolmuş. Lütfen tekrar giriş yapın." });
        }

        if (profileImage == null || profileImage.Length == 0)
        {
            return Json(new { success = false, message = "Dosya seçilmedi" });
        }

        try
        {
            Console.WriteLine($"📸 UploadProfileImage started for user: {username} (ID: {userId})");
            
            var user = await _userService.GetUserByUsernameAsync(username);
            if (user == null)
            {
                Console.WriteLine($"❌ User not found: {username}");
                return Json(new { success = false, message = "Kullanıcı bulunamadı" });
            }

            // Eski profil fotoğrafını sil
            if (!string.IsNullOrEmpty(user.ProfileImageUrl) && 
                !user.ProfileImageUrl.Contains("ui-avatars.com"))
            {
                await _s3Service.DeleteImageAsync(user.ProfileImageUrl);
                Console.WriteLine($"🗑️ Old profile image deleted for user: {username}");
            }

            // Yeni profil fotoğrafını yükle
            var imageUrl = await _s3Service.UploadProfileImageAsync(profileImage, user.Id);
            Console.WriteLine($"✅ New profile image uploaded: {imageUrl}");
            
            // Veritabanını güncelle
            user.ProfileImageUrl = imageUrl;
            await _userService.UpdateUserAsync(user);
            Console.WriteLine($"✅ Profile image updated successfully for user: {username}");

            return Json(new { 
                success = true, 
                message = "Profil fotoğrafı başarıyla güncellendi",
                imageUrl = imageUrl
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ UploadProfileImage error: {ex.Message}");
            return Json(new { success = false, message = ex.Message });
        }
    }

    [HttpDelete]
    public async Task<IActionResult> DeleteProfileImage()
    {
        // Claims'den kullanıcı bilgilerini al
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        var username = User.Identity?.Name ?? "";
        
        if (userId == 0)
        {
            Console.WriteLine("❌ DeleteProfileImage: Invalid user session");
            return Json(new { success = false, message = "Oturum süreniz dolmuş. Lütfen tekrar giriş yapın." });
        }

        try
        {
            Console.WriteLine($"🗑️ DeleteProfileImage started for user: {username} (ID: {userId})");
            
            var user = await _userService.GetUserByUsernameAsync(username);
            if (user == null)
            {
                Console.WriteLine($"❌ DeleteProfileImage: User not found - {username}");
                return Json(new { success = false, message = "Kullanıcı bulunamadı" });
            }

            // Profil fotoğrafını S3'ten sil
            if (!string.IsNullOrEmpty(user.ProfileImageUrl) && 
                !user.ProfileImageUrl.Contains("ui-avatars.com"))
            {
                await _s3Service.DeleteImageAsync(user.ProfileImageUrl);
                Console.WriteLine($"🗑️ Old profile image deleted from S3 for user: {username}");
            }

            // Veritabanından profil fotoğrafını kaldır
            user.ProfileImageUrl = null;
            await _userService.UpdateUserAsync(user);
            Console.WriteLine($"✅ Profile image deleted successfully for user: {username}");

            var defaultAvatarUrl = _s3Service.GetDefaultAvatarUrl();

            return Json(new { 
                success = true, 
                message = "Profil fotoğrafı silindi",
                defaultImageUrl = defaultAvatarUrl
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ DeleteProfileImage error: {ex.Message}");
            return Json(new { success = false, message = $"Profil fotoğrafı silinemedi: {ex.Message}" });
        }
    }

    private static string? NormalizeStatus(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        switch(raw.Trim().ToLowerInvariant())
        {
            case "online": return UserStatusOptions.Online;
            case "busy": return UserStatusOptions.Busy;
            case "away": return UserStatusOptions.Away;
            case "offline": return UserStatusOptions.Offline;
            default: return null;
        }
    }

    [HttpPost]
    public async Task<IActionResult> UpdateStatus(string status)
    {
        // Claims'den kullanıcı bilgilerini al
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        var username = User.Identity?.Name ?? "";
        
        if (userId == 0)
        {
            Console.WriteLine("❌ UpdateStatus: Invalid user session");
            return Json(new { success = false, message = "Oturum süreniz dolmuş. Lütfen tekrar giriş yapın." });
        }
        var normalized = NormalizeStatus(status);
        if (normalized == null) return Json(new { success=false, message="Geçersiz durum" });

        try
        {
            Console.WriteLine($"🔄 UpdateStatus started for user: {username} (ID: {userId}) - Status: {normalized}");
            
            var user = await _userService.GetUserByUsernameAsync(username);
            if (user == null)
            {
                Console.WriteLine($"❌ UpdateStatus: User not found - {username}");
                return Json(new { success = false, message = "Kullanıcı bulunamadı" });
            }

            // String durumu kontrol et
            user.UserStatus = normalized;
            user.IsOnline = normalized != UserStatusOptions.Offline;

            await _userService.UpdateUserAsync(user);
            Console.WriteLine($"✅ Status updated successfully for user: {username} - New status: {normalized}");

            return Json(new { 
                success = true, 
                message = "Durum güncellendi",
                status = normalized
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ UpdateStatus error: {ex.Message}");
            return Json(new { success = false, message = $"Durum güncellenemedi: {ex.Message}" });
        }
    }

    // Dünya ülkeleri - Database'den
    [HttpGet]
    public async Task<IActionResult> GetWorldCountries()
    {
        try
        {
            var locationService = HttpContext.RequestServices.GetRequiredService<ILocationService>();
            var countries = await locationService.GetAllCountriesAsync();
            
            var countryList = countries.Select(c => new { 
                id = c.Id,
                name = c.Name 
            }).Cast<object>().ToList();

            Console.WriteLine($"✅ Loaded {countryList.Count} countries from database");
            Response.Headers["Cache-Control"] = "public, max-age=21600"; // 6 saat
            return Json(new { success = true, data = countryList });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ GetWorldCountries error: {ex.Message}");
            return Json(new { success = false, message = "Ülkeler yüklenemedi" });
        }
    }

    // Belirli ülke şehirleri - Database'den
    [HttpGet]
    public async Task<IActionResult> GetWorldCities(int countryId)
    {
        try
        {
            if (countryId <= 0)
            {
                Console.WriteLine("❌ GetWorldCities: Invalid countryId received");
                return Json(new { success = false, message = "Ülke ID gerekli" });
            }

            Console.WriteLine($"🏙️ GetWorldCities called with countryId: {countryId}");

            var locationService = HttpContext.RequestServices.GetRequiredService<ILocationService>();
            
            // Direkt countryId ile şehirleri çek
            var cities = await locationService.GetCitiesByCountryAsync(countryId);
            var cityList = cities.Select(c => new { 
                id = c.Id,
                name = c.Name 
            }).Cast<object>().ToList();

            Console.WriteLine($"✅ Loaded {cityList.Count} cities for countryId: {countryId}");
            Response.Headers["Cache-Control"] = "public, max-age=21600";
            return Json(new { success = true, data = cityList });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ GetWorldCities error: {ex.Message}");
            Console.WriteLine($"❌ Stack trace: {ex.StackTrace}");
            return Json(new { success = false, message = "Şehirler yüklenemedi" });
        }
    }

    // JSON için DTO
    public class CountryDto
    {
        public string Name { get; set; } = "";
    }
}
