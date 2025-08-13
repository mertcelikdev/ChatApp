using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ChatApp.Services;
using ChatApp.Models;
using System.Security.Claims;

namespace ChatApp.Controllers
{
    [Authorize(Policy = "AdminOnly")]
    public class AdminController : Controller
    {
        private readonly IUserService _userService;
        private readonly IS3Service _s3Service;

        public AdminController(IUserService userService, IS3Service s3Service)
        {
            _userService = userService;
            _s3Service = s3Service;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            // Kullanıcı bilgilerini Claims'den al
            var username = User.Identity?.Name ?? "";
            var user = await _userService.GetUserByUsernameAsync(username);
            
            // Navbar için profil bilgisini ekle
            ViewBag.ProfileImageUrl = user?.ProfileImageUrl;
            
            var users = await _userService.GetAllUsersAsync();
            return View(users);
        }

        [HttpGet]
        public async Task<IActionResult> UserDetails(int id)
        {
            var user = await _userService.GetUserByIdAsync(id);
            if (user == null)
            {
                TempData["ErrorMessage"] = "Kullanıcı bulunamadı!";
                return RedirectToAction("Index");
            }
            return View(user);
        }

        [HttpGet]
        public IActionResult CreateUser()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> CreateUser(string username, string email, string password, string displayName, string? bio = null, string? location = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(displayName))
                {
                    displayName = username;
                }

                var user = await _userService.CreateUserAsync(username, password);
                
                // Email ve DisplayName'i ayrıca set et
                user.Email = email;
                user.DisplayName = displayName;
                
                // Opsiyonel alanları güncelle
                if (!string.IsNullOrWhiteSpace(bio) || !string.IsNullOrWhiteSpace(location))
                {
                    user.Bio = bio;
                    user.Location = location;
                }
                
                await _userService.UpdateUserAsync(user);

                TempData["SuccessMessage"] = $"Kullanıcı '{username}' başarıyla oluşturuldu!";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Kullanıcı oluştururken hata: " + ex.Message;
                return View();
            }
        }

        [HttpGet]
        public async Task<IActionResult> EditUser(int id)
        {
            var user = await _userService.GetUserByIdAsync(id);
            if (user == null)
            {
                TempData["ErrorMessage"] = "Kullanıcı bulunamadı!";
                return RedirectToAction("Index");
            }
            return View(user);
        }

        [HttpPost]
        public async Task<IActionResult> EditUser(int id, string username, string email, string displayName, string? bio = null, string? location = null, string? userStatus = null)
        {
            try
            {
                var user = await _userService.GetUserByIdAsync(id);
                if (user == null)
                {
                    TempData["ErrorMessage"] = "Kullanıcı bulunamadı!";
                    return RedirectToAction("Index");
                }

                user.Username = username;
                user.Email = email;
                user.DisplayName = displayName;
                user.Bio = bio;
                user.Location = location;
                
                if (!string.IsNullOrEmpty(userStatus) && UserStatusOptions.AllStatuses.Contains(userStatus))
                {
                    user.UserStatus = userStatus;
                }

                await _userService.UpdateUserAsync(user);
                TempData["SuccessMessage"] = "Kullanıcı bilgileri güncellendi!";
                return RedirectToAction("UserDetails", new { id });
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Güncelleme hatası: " + ex.Message;
                return View();
            }
        }

        [HttpPost]
        public async Task<IActionResult> DeleteUser(int id)
        {
            try
            {
                var user = await _userService.GetUserByIdAsync(id);
                if (user == null)
                {
                    return Json(new { success = false, message = "Kullanıcı bulunamadı!" });
                }

                // Admin kendini silemez
                var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
                if (id == currentUserId)
                {
                    return Json(new { success = false, message = "Kendi hesabınızı silemezsiniz!" });
                }

                // Profil resmini S3'ten sil
                if (!string.IsNullOrEmpty(user.ProfileImageUrl) && user.ProfileImageUrl.Contains("amazonaws.com"))
                {
                    try
                    {
                        await _s3Service.DeleteImageAsync(user.ProfileImageUrl);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"S3 resim silme hatası: {ex.Message}");
                    }
                }

                await _userService.DeleteUserAsync(id);
                return Json(new { success = true, message = $"Kullanıcı '{user.Username}' silindi!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Silme hatası: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> ResetPassword(int id, string newPassword)
        {
            try
            {
                var user = await _userService.GetUserByIdAsync(id);
                if (user == null)
                {
                    return Json(new { success = false, message = "Kullanıcı bulunamadı!" });
                }

                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
                await _userService.UpdateUserAsync(user);

                return Json(new { success = true, message = "Şifre başarıyla sıfırlandı!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Şifre sıfırlama hatası: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> ToggleUserStatus(int id)
        {
            try
            {
                var user = await _userService.GetUserByIdAsync(id);
                if (user == null)
                {
                    return Json(new { success = false, message = "Kullanıcı bulunamadı!" });
                }

                // Kullanıcı artık aktif/deaktif durumu yok - sadece offline yap
                user.IsOnline = false;

                await _userService.UpdateUserAsync(user);

                var status = true ? "aktif" : "deaktif";
                return Json(new { success = true, message = $"Kullanıcı {status} duruma getirildi!", isActive = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Durum değiştirme hatası: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> ForceLogout(int id)
        {
            try
            {
                var user = await _userService.GetUserByIdAsync(id);
                if (user == null)
                {
                    return Json(new { success = false, message = "Kullanıcı bulunamadı!" });
                }

                await _userService.SetUserOfflineAsync(id);
                return Json(new { success = true, message = $"Kullanıcı '{user.Username}' zorla çıkış yaptırıldı!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Zorla çıkış hatası: " + ex.Message });
            }
        }

        // API endpoint'leri
        [HttpGet]
        public async Task<IActionResult> GetUsers()
        {
            try
            {
                var users = await _userService.GetAllUsersAsync();
                var userList = users.Select(u => new
                {
                    id = u.Id,
                    username = u.Username,
                    email = u.Email,
                    displayName = u.DisplayName,
                    isOnline = u.IsOnline,
                    isActive = true,
                    createdAt = u.CreatedAt.ToString("dd.MM.yyyy HH:mm"),
                    lastLoginAt = u.LastLoginAt?.ToString("dd.MM.yyyy HH:mm") ?? "Hiç giriş yapmamış"
                });

                return Json(new { success = true, users = userList });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Kullanıcılar yüklenirken hata: " + ex.Message });
            }
        }
    }
}
