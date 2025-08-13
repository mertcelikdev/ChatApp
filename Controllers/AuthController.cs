using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;
using ChatApp.Services;
using ChatApp.Models;

namespace ChatApp.Controllers
{
    public class AuthController : Controller
    {
        private readonly IUserService _userService;

        public AuthController(IUserService userService)
        {
            _userService = userService;
        }

        [HttpGet]
        public IActionResult Login()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Index", "Chat");
            }
            // Artık anasayfadan giriş yapıyoruz, o yüzden Home/Index'e yönlendir
            return RedirectToAction("Index", "Home");
        }

        [HttpPost]
        public async Task<IActionResult> Login(string username, string password, bool rememberMe = false)
        {
            try
            {
                var user = await _userService.AuthenticateUserAsync(username, password);
                
                if (user != null)
                {
                    var claims = new List<Claim>
                    {
                        new Claim(ClaimTypes.Name, user.Username),
                        new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                        new Claim("DisplayName", user.DisplayName ?? user.Username),
                        new Claim("ProfileImage", user.ProfileImageUrl ?? ""),
                        new Claim("Role", user.Username == "admin" ? "Admin" : "User")
                    };

                    var claimsIdentity = new ClaimsIdentity(claims, "CookieAuth");
                    var authProperties = new AuthenticationProperties
                    {
                        IsPersistent = rememberMe,
                        ExpiresUtc = rememberMe ? DateTimeOffset.UtcNow.AddDays(30) : DateTimeOffset.UtcNow.AddMinutes(30)
                    };

                    await HttpContext.SignInAsync("CookieAuth", new ClaimsPrincipal(claimsIdentity), authProperties);

                    // Kullanıcının son giriş tarihini güncelle
                    await _userService.UpdateLastLoginAsync(user.Id);

                    return RedirectToAction("Index", "Chat");
                }

                ViewBag.ErrorMessage = "Geçersiz kullanıcı adı veya şifre!";
                return View();
            }
            catch (Exception ex)
            {
                ViewBag.ErrorMessage = "Giriş sırasında bir hata oluştu: " + ex.Message;
                return View();
            }
        }

        [HttpGet]
        public IActionResult Register()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Index", "Chat");
            }
            // Artık anasayfadan kayıt oluyoruz, o yüzden Home/Index'e yönlendir
            return RedirectToAction("Index", "Home");
        }

        [HttpPost]
        public async Task<IActionResult> Register(string username, string email, string password, string confirmPassword, string displayName = "")
        {
            try
            {
                if (password != confirmPassword)
                {
                    ViewBag.ErrorMessage = "Şifreler eşleşmiyor!";
                    return View();
                }

                if (string.IsNullOrWhiteSpace(displayName))
                {
                    displayName = username;
                }

                var user = await _userService.CreateUserAsync(username, password);
                
                // Email ve DisplayName'i ayrıca set et
                user.Email = email;
                user.DisplayName = displayName;
                await _userService.UpdateUserAsync(user);
                
                if (user != null)
                {
                    ViewBag.SuccessMessage = "Hesabınız başarıyla oluşturuldu! Şimdi giriş yapabilirsiniz.";
                    return View("Login");
                }

                ViewBag.ErrorMessage = "Kullanıcı oluşturulurken bir hata oluştu!";
                return View();
            }
            catch (Exception ex)
            {
                ViewBag.ErrorMessage = "Kayıt sırasında bir hata oluştu: " + ex.Message;
                return View();
            }
        }

        [HttpPost]
        [HttpGet]
        public async Task<IActionResult> Logout()
        {
            try
            {
                Console.WriteLine("🚪 Logout process started");
                
                // Kullanıcının online durumunu güncelle
                if (User.Identity?.IsAuthenticated == true)
                {
                    var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
                    var username = User.Identity?.Name ?? "";
                    
                    Console.WriteLine($"🔍 Logging out user: {username} (ID: {userId})");
                    
                    if (userId > 0)
                    {
                        await _userService.SetUserOfflineAsync(userId);
                        Console.WriteLine($"✅ User {username} set to offline");
                    }
                }

                // Session'ı temizle
                HttpContext.Session.Clear();
                Console.WriteLine("🧹 Session cleared");

                // Authentication cookie'sini temizle
                await HttpContext.SignOutAsync("CookieAuth");
                Console.WriteLine("🔐 Authentication cookie cleared");

                // AJAX request ise JSON response döndür
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = true, message = "Başarıyla çıkış yapıldı" });
                }

                return RedirectToAction("Login");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Logout error: {ex.Message}");
                
                // Hata olsa bile temizlik yap
                try
                {
                    HttpContext.Session.Clear();
                    await HttpContext.SignOutAsync("CookieAuth");
                }
                catch (Exception cleanupEx)
                {
                    Console.WriteLine($"❌ Cleanup error: {cleanupEx.Message}");
                }

                // AJAX request ise JSON response döndür
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = true, message = "Çıkış yapıldı (hata ile)" });
                }

                return RedirectToAction("Login");
            }
        }

        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}
