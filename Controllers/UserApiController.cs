namespace ChatApp.Controllers;

using Microsoft.AspNetCore.Mvc;
using ChatApp.Models;
using ChatApp.Services;
using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;

[ApiController]
[Route("api/[controller]")]
public class UserApiController : ControllerBase
{
    private readonly IUserService _userService;

    public UserApiController(IUserService userService)
    {
        _userService = userService;
    }

    // 🔥 Kullanıcı girişi (async)
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username))
        {
            return BadRequest(new { success = false, message = "Kullanıcı adı boş olamaz" });
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { success = false, message = "Şifre boş olamaz" });
        }

        try
        {
            var username = request.Username.Trim();
            
            // Kullanıcı adı ve şifre doğrulama
            var existingUser = await _userService.ValidateUserAsync(username, request.Password);
            if (existingUser == null)
            {
                return Unauthorized(new { success = false, message = "Kullanıcı adı veya şifre hatalı" });
            }

            // Kullanıcı zaten aktif mi kontrol et
            var activeSessions = await _userService.GetActiveSessionsAsync(existingUser.Id);
            if (activeSessions.Any())
            {
                return BadRequest(new { success = false, message = "Bu kullanıcı zaten giriş yapmış" });
            }

            // Yeni session oluştur
            var session = await _userService.CreateSessionAsync(existingUser.Id, request.ConnectionId);

            // ASP.NET Core Authentication sistemini kullan
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, existingUser.Username),
                new Claim(ClaimTypes.NameIdentifier, existingUser.Id.ToString()),
                new Claim("DisplayName", existingUser.DisplayName ?? existingUser.Username),
                new Claim("ProfileImage", existingUser.ProfileImageUrl ?? ""),
                new Claim("Role", existingUser.Username == "admin" ? "Admin" : "User")
            };

            var claimsIdentity = new ClaimsIdentity(claims, "CookieAuth");
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = true, // Remember me
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(24)
            };

            await HttpContext.SignInAsync("CookieAuth", new ClaimsPrincipal(claimsIdentity), authProperties);

            // Kullanıcının son giriş tarihini güncelle
            await _userService.UpdateLastLoginAsync(existingUser.Id);

            // Eski cookie de set et (uyumluluk için)
            var cookieOptions = new CookieOptions
            {
                HttpOnly = false, // JavaScript'ten erişilebilir olması için
                Secure = false, // HTTPS için true yapılabilir
                SameSite = SameSiteMode.Lax,
                Expires = DateTime.UtcNow.AddHours(24)
            };
            Response.Cookies.Append("chatapp_username", existingUser.Username, cookieOptions);

            // Online kullanıcı listesini getir
            var onlineUsers = await _userService.GetActiveUsersAsync();

            Console.WriteLine($"👤 User logged in: {username} - Auth cookie set");

            return Ok(new { 
                success = true, 
                message = "Giriş başarılı",
                user = new { 
                    id = existingUser.Id,
                    username = existingUser.Username,
                    profileImageUrl = existingUser.ProfileImageUrl,
                    loginTime = session.LoginTime,
                    sessionId = session.Id
                },
                onlineUsers = onlineUsers
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, message = $"Giriş sırasında hata: {ex.Message}" });
        }
    }

    // 🔥 Kullanıcı kaydı (async)
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username))
        {
            return BadRequest(new { success = false, message = "Kullanıcı adı boş olamaz" });
        }

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return BadRequest(new { success = false, message = "E-posta boş olamaz" });
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { success = false, message = "Şifre boş olamaz" });
        }

        if (request.Username.Length < 3 || request.Username.Length > 20)
        {
            return BadRequest(new { success = false, message = "Kullanıcı adı 3-20 karakter arası olmalıdır" });
        }

        if (request.Password.Length < 6)
        {
            return BadRequest(new { success = false, message = "Şifre en az 6 karakter olmalıdır" });
        }

        try
        {
            var username = request.Username.Trim();
            var email = request.Email.Trim();
            
            // Kullanıcı adı kontrolü
            var existingUser = await _userService.GetUserByUsernameAsync(username);
            if (existingUser != null)
            {
                return BadRequest(new { success = false, message = "Bu kullanıcı adı zaten alınmış" });
            }

            // Yeni kullanıcı oluştur
            var newUser = await _userService.CreateUserAsync(username, request.Password, request.ProfileImageUrl);
            
            // Email'i ayrıca set edelim
            newUser.Email = email;
            
            // Kullanıcıyı online yap ve session oluştur (register sırasında otomatik login)
            newUser.IsOnline = true;
            newUser.LastLoginAt = DateTime.UtcNow;
            await _userService.UpdateUserAsync(newUser);
            
            // Session oluştur
            var session = await _userService.CreateSessionAsync(newUser.Id, "register-auto-login");
            
            // ASP.NET Core Authentication sistemini kullan
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, newUser.Username),
                new Claim(ClaimTypes.NameIdentifier, newUser.Id.ToString()),
                new Claim("DisplayName", newUser.DisplayName ?? newUser.Username),
                new Claim("ProfileImage", newUser.ProfileImageUrl ?? ""),
                new Claim("Role", newUser.Username == "admin" ? "Admin" : "User")
            };

            var claimsIdentity = new ClaimsIdentity(claims, "CookieAuth");
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = true, // Remember me
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(24)
            };

            await HttpContext.SignInAsync("CookieAuth", new ClaimsPrincipal(claimsIdentity), authProperties);

            // Eski cookie de set et (uyumluluk için)
            var cookieOptions = new CookieOptions
            {
                HttpOnly = false, // JavaScript'ten erişilebilir olması için
                Secure = false, // HTTPS için true yapılabilir
                SameSite = SameSiteMode.Lax,
                Expires = DateTime.UtcNow.AddHours(24)
            };
            Response.Cookies.Append("chatapp_username", newUser.Username, cookieOptions);
            
            Console.WriteLine($"🎉 Yeni kullanıcı kaydı ve otomatik giriş: {username} ({email}) - Auth cookie set");

            return Ok(new { 
                success = true, 
                message = "Kayıt başarılı! Hoş geldiniz!",
                user = new { 
                    id = newUser.Id, 
                    username = newUser.Username,
                    email = newUser.Email,
                    createdAt = newUser.CreatedAt
                }
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, message = $"Kayıt sırasında hata: {ex.Message}" });
        }
    }

    // E-posta formatı kontrolü
    private static bool IsValidEmail(string email)
    {
        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }

    // 🔥 Kullanıcı çıkışı (async)
    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username))
        {
            return BadRequest(new { success = false, message = "Kullanıcı adı boş olamaz" });
        }

        try
        {
            var username = request.Username.Trim();
            var user = await _userService.GetUserByUsernameAsync(username);
            
            if (user != null)
            {
                // Kullanıcının tüm aktif session'larını kapat
                await _userService.EndSessionAsync(user.Id);
                
                // User'ı offline yap
                user.IsOnline = false;
                user.LastLoginAt = DateTime.UtcNow;
                await _userService.UpdateUserAsync(user);
                
                // Cookie'yi temizle
                Response.Cookies.Delete("chatapp_username");
                
                Console.WriteLine($"👤 User logged out: {username} - Cookie cleared");

                return Ok(new { 
                    success = true, 
                    message = "Çıkış başarılı",
                    username = username
                });
            }
            else
            {
                return NotFound(new { success = false, message = "Kullanıcı bulunamadı" });
            }
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, message = $"Çıkış sırasında hata: {ex.Message}" });
        }
    }

    // 🔥 Online kullanıcıları getir
    [HttpGet("online-users")]
    public async Task<IActionResult> GetOnlineUsers()
    {
        try
        {
            var onlineUsers = await _userService.GetActiveUsersAsync();
            
            return Ok(new { 
                success = true, 
                users = onlineUsers,
                count = onlineUsers.Count()
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, message = $"Kullanıcı listesi alınırken hata: {ex.Message}" });
        }
    }

    // 🔥 Public chat mesajlarını getir (async)
    [HttpGet("public-messages")]
    public async Task<IActionResult> GetPublicMessages()
    {
        try
        {
            var messages = await _userService.GetPublicMessagesAsync(50);
            return Ok(new { success = true, messages = messages });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, message = $"Public mesajlar alınırken hata: {ex.Message}" });
        }
    }

    // 🔥 Kullanıcıya özel mesajları getir (async)
    [HttpGet("private-messages/{username}")]
    public async Task<IActionResult> GetPrivateMessages(string username, [FromQuery] string? withUser = null)
    {
        try
        {
            var user = await _userService.GetUserByUsernameAsync(username);
            if (user == null)
            {
                return Unauthorized(new { success = false, message = "Kullanıcı bulunamadı" });
            }

            var messages = await _userService.GetPrivateMessagesAsync(user.Id, withUser, 50);
            return Ok(new { success = true, messages = messages });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, message = $"Private mesajlar alınırken hata: {ex.Message}" });
        }
    }

    // 🔥 Public mesaj kaydet (async)
    [HttpPost("save-public-message")]
    public async Task<IActionResult> SavePublicMessage([FromBody] ChatMessage message)
    {
        try
        {
            var user = await _userService.GetUserByUsernameAsync(message.From);
            if (user == null)
            {
                return Unauthorized(new { success = false, message = "Kullanıcı giriş yapmamış" });
            }

            await _userService.SavePublicMessageAsync(user.Id, message.Message);

            Console.WriteLine($"📢 Public message saved: {message.From} - {message.Message}");
            return Ok(new { success = true, message = "Mesaj kaydedildi" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, message = $"Mesaj kaydedilirken hata: {ex.Message}" });
        }
    }

    // 🔥 Private mesaj kaydet (async)
    [HttpPost("save-private-message")]
    public async Task<IActionResult> SavePrivateMessage([FromBody] ChatMessage message)
    {
        try
        {
            var fromUser = await _userService.GetUserByUsernameAsync(message.From);
            var toUser = await _userService.GetUserByUsernameAsync(message.To);

            if (fromUser == null)
            {
                return Unauthorized(new { success = false, message = "Gönderen kullanıcı bulunamadı" });
            }

            if (toUser == null)
            {
                return BadRequest(new { success = false, message = "Alıcı kullanıcı bulunamadı" });
            }

            await _userService.SavePrivateMessageAsync(fromUser.Id, toUser.Id, message.Message);

            Console.WriteLine($"📨 Private message saved: {message.From} → {message.To}");
            return Ok(new { success = true, message = "Özel mesaj kaydedildi" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, message = $"Özel mesaj kaydedilirken hata: {ex.Message}" });
        }
    }

    // 🔥 Kullanıcı durumunu kontrol et (async)
    [HttpGet("status/{username}")]
    public async Task<IActionResult> CheckUserStatus(string username)
    {
        try
        {
            var user = await _userService.GetUserByUsernameAsync(username);
            if (user == null)
            {
                return Ok(new { 
                    success = true, 
                    username = username,
                    isOnline = false
                });
            }

            var activeSessions = await _userService.GetActiveSessionsAsync(user.Id);
            var isOnline = activeSessions.Any();
            var latestSession = activeSessions.OrderByDescending(s => s.LoginTime).FirstOrDefault();

            return Ok(new { 
                success = true, 
                username = username,
                isOnline = isOnline,
                loginTime = latestSession?.LoginTime,
                connectionId = latestSession?.ConnectionId
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, message = $"Kullanıcı durumu kontrol edilirken hata: {ex.Message}" });
        }
    }

    // 🔥 Zorla logout (debug için)
    [HttpPost("force-logout")]
    public async Task<IActionResult> ForceLogout([FromBody] LogoutRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username))
        {
            return BadRequest(new { success = false, message = "Kullanıcı adı boş olamaz" });
        }

        try
        {
            await _userService.ForceLogoutUserAsync(request.Username.Trim());
            
            return Ok(new { 
                success = true, 
                message = $"Kullanıcı {request.Username} zorla offline yapıldı",
                username = request.Username
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, message = $"Force logout sırasında hata: {ex.Message}" });
        }
    }

    // 🔥 Debug - tüm kullanıcıları offline yap
    [HttpPost("debug-offline-all")]
    public async Task<IActionResult> DebugOfflineAll()
    {
        try
        {
            var message = await _userService.DebugOfflineAllUsersAsync();
            
            return Ok(new { 
                success = true, 
                message = message
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, message = $"Debug offline all sırasında hata: {ex.Message}" });
        }
    }
}
