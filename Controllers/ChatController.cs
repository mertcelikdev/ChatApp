using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ChatApp.Services;
using ChatApp.Data;
using ChatApp.Constants;
using ChatApp.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Microsoft.AspNetCore.Hosting;


namespace ChatApp.Controllers
{
    [Authorize]
    public class ChatController : Controller
    {
        private readonly IUserService _userService;
        private readonly ChatDbContext _context;
        private readonly IEncryptionService _encryptionService;
        private readonly IGroupService _groupService;
        private readonly IWebHostEnvironment _env;

        public ChatController(
            IUserService userService,
            ChatDbContext context,
            IEncryptionService encryptionService,
            IGroupService groupService,
            IWebHostEnvironment env)
        {
            _userService = userService;
            _context = context;
            _encryptionService = encryptionService;
            _groupService = groupService;
            _env = env;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            // Kullanıcı bilgilerini Claims'den al
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            var username = User.Identity?.Name ?? "";
            
            if (userId == 0)
            {
                return RedirectToAction("Index", "Home");
            }

            // Kullanıcı bilgilerini al
            var user = await _userService.GetUserByUsernameAsync(username);
            
            // Kullanıcıyı online yap
            await _userService.SetUserOnlineAsync(userId);
            
            // Kullanıcı bilgilerini ViewBag'e ekle
            ViewBag.Username = username;
            ViewBag.UserId = userId;
            ViewBag.DisplayName = User.FindFirst("DisplayName")?.Value ?? username;
            ViewBag.ProfileImageUrl = user?.ProfileImageUrl;
            ViewBag.IsAdmin = User.HasClaim("Role", "Admin");
            ViewBag.Title = "ChatApp - Sohbet Dashboard";
            
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> Private()
        {
            // Kullanıcı bilgilerini Claims'den al
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            var username = User.Identity?.Name ?? "";
            
            if (userId == 0)
            {
                return RedirectToAction("Login", "Auth");
            }

            // Kullanıcı bilgilerini al
            var user = await _userService.GetUserByUsernameAsync(username);

            // Kullanıcı bilgilerini ViewBag'e ekle
            ViewBag.Username = username;
            ViewBag.DisplayName = User.FindFirst("DisplayName")?.Value ?? username;
            ViewBag.ProfileImageUrl = user?.ProfileImageUrl;
            ViewBag.Title = "ChatApp - Özel Mesajlaşma";
            
            return View();
        }

        [HttpGet]
        public IActionResult General()
        {
            // Kullanıcı giriş yapmış mı kontrol et
            var username = Request.Cookies["chatapp_username"];
            
            if (string.IsNullOrEmpty(username))
            {
                // Giriş yapmamışsa anasayfaya yönlendir
                return RedirectToAction("Index", "Home");
            }

            // Kullanıcı bilgilerini ViewBag'e ekle
            ViewBag.Username = username;
            ViewBag.Title = "ChatApp - Genel Sohbet";
            
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> Public()
        {
            // Kullanıcı bilgilerini Claims'den al
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            var username = User.Identity?.Name ?? "";
            
            if (userId == 0)
            {
                return RedirectToAction("Login", "Auth");
            }

            // Kullanıcı bilgilerini al
            var user = await _userService.GetUserByUsernameAsync(username);

            // Kullanıcı bilgilerini ViewBag'e ekle
            ViewBag.Username = username;
            ViewBag.ProfileImageUrl = user?.ProfileImageUrl;
            ViewBag.Title = "ChatApp - Genel Sohbet";
            
            return View();
        }

        // Özel mesaj geçmişini getir
        [HttpGet]
        public async Task<IActionResult> GetPrivateMessages(string otherUser)
        {
            // Kullanıcı bilgilerini Claims'den al
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            var username = User.Identity?.Name ?? "";
            
            if (userId == 0)
            {
                return Json(new { success = false, message = "Oturum açın" });
            }
            
            if (string.IsNullOrEmpty(otherUser))
            {
                return Json(new { success = false, message = "Kullanıcı belirtilmedi" });
            }

            try
            {
                // Mesaj tipi karşılaştırmasını case-insensitive yapıyoruz ("Private" / "private")
                var messages = await _context.ChatMessages
                    .Where(m => m.MessageType.ToLower() == "private" && 
                               ((m.From == username && m.To == otherUser) || 
                                (m.From == otherUser && m.To == username)))
                    .OrderBy(m => m.Timestamp)
                    .ToListAsync();

                // Kullanıcı profil fotoğraflarını almak için
                var fromUser = await _userService.GetUserByUsernameAsync(username);
                var otherUserInfo = await _userService.GetUserByUsernameAsync(otherUser);

                // Mesajları şifre çözerek döndür
                var decryptedMessages = messages.Select(m => new {
                    id = m.Id,
                    from = m.From,
                    to = m.To,
                    message = _encryptionService.Decrypt(m.Message), // Şifre çöz
                    timestamp = m.Timestamp,
                    profileImageUrl = m.From == username ? fromUser?.ProfileImageUrl : otherUserInfo?.ProfileImageUrl
                }).ToList();

                Console.WriteLine($"📖 Private messages loaded: {messages.Count} messages between {username} and {otherUser}");

                return Json(new { success = true, messages = decryptedMessages });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error loading private messages: {ex.Message}");
                return Json(new { success = false, message = "Mesajlar yüklenemedi: " + ex.Message });
            }
        }

        // Sohbet listesi (konuşma bazlı) getir
        [HttpGet]
        public async Task<IActionResult> GetConversations()
        {
            var username = User.Identity?.Name ?? string.Empty;
            if (string.IsNullOrEmpty(username))
            {
                return Json(new { success = false, message = "Oturum açın" });
            }

            try
            {
                var privateMessages = await _context.ChatMessages
                    .Where(m => m.MessageType.ToLower() == "private" && (m.From == username || m.To == username))
                    .OrderByDescending(m => m.Timestamp)
                    .ToListAsync();

                var conversationGroups = privateMessages
                    .GroupBy(m => m.From == username ? m.To : m.From)
                    .Select(g => new
                    {
                        OtherUser = g.Key,
                        LastMessageEntity = g.OrderByDescending(m => m.Timestamp).First()
                    })
                    .ToList();

                // Gerekli kullanıcı bilgilerini topla
                var partnerUsernames = conversationGroups.Select(c => c.OtherUser).Distinct().ToList();
                var partnerInfos = new Dictionary<string, (string? displayName, string? profileImageUrl, bool isOnline)>();
                foreach (var u in partnerUsernames)
                {
                    var usr = await _userService.GetUserByUsernameAsync(u);
                    if (usr != null)
                    {
                        partnerInfos[u] = (usr.DisplayName, usr.ProfileImageUrl, usr.IsOnline);
                    }
                    else
                    {
                        partnerInfos[u] = (null, null, false);
                    }
                }

                var conversations = conversationGroups.Select(c => new {
                    username = c.OtherUser,
                    displayName = partnerInfos[c.OtherUser].displayName ?? c.OtherUser,
                    profileImageUrl = partnerInfos[c.OtherUser].profileImageUrl,
                    isOnline = partnerInfos[c.OtherUser].isOnline,
                    lastMessage = _encryptionService.Decrypt(c.LastMessageEntity.Message),
                    lastMessageTimestamp = c.LastMessageEntity.Timestamp,
                    unreadCount = privateMessages.Count(m => m.From == c.OtherUser && m.To == username && !m.IsRead)
                })
                .OrderByDescending(c => c.lastMessageTimestamp)
                .ToList();

                return Json(new { success = true, conversations });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error loading conversations: {ex.Message}");
                return Json(new { success = false, message = "Sohbet listesi yüklenemedi: " + ex.Message });
            }
        }

        // Public mesaj geçmişini getir
        [HttpGet]
        public async Task<IActionResult> GetPublicMessages()
        {
            try
            {
                var messages = await _context.ChatMessages
                    .Where(m => m.MessageType == MessageTypes.PUBLIC && m.ToUserId == SystemUsers.GENERAL_CHAT_USER_ID)
                    .OrderBy(m => m.Timestamp)
                    .ToListAsync();

                // Kullanıcı profil fotoğraflarını almak için username'leri topla
                var usernames = messages.Select(m => m.From).Distinct().ToList();
                var users = new Dictionary<string, string>();
                
                foreach (var username in usernames)
                {
                    var user = await _userService.GetUserByUsernameAsync(username);
                    users[username] = user?.ProfileImageUrl ?? "";
                }

                // Mesajları şifre çözerek döndür
                var decryptedMessages = messages.Select(m => new {
                    from = m.From,
                    message = _encryptionService.Decrypt(m.Message), // Şifre çöz
                    timestamp = m.Timestamp,
                    profileImageUrl = users.ContainsKey(m.From) ? users[m.From] : ""
                }).ToList();

                return Json(new { success = true, messages = decryptedMessages });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Mesajlar yüklenemedi: " + ex.Message });
            }
        }

        // Kullanıcı listesini getir (mesajlaşma için)
        [HttpGet]
        public async Task<IActionResult> GetUsers()
        {
            // Kullanıcı bilgilerini Claims'den al
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            var username = User.Identity?.Name ?? "";
            
            if (userId == 0)
            {
                return Json(new { success = false, message = "Oturum açın" });
            }

            try
            {
                var users = await _userService.GetAllUsersAsync();
                
                var userList = users
                    .Where(u => u.Username != username) // Kendisini hariç tut
                    .Select(u => new {
                        username = u.Username,
                        displayName = u.DisplayName,
                        profileImageUrl = u.ProfileImageUrl,
                        isOnline = u.IsOnline,
                        userStatus = u.UserStatus,
                        lastLoginAt = u.LastLoginAt,
                        isActive = true
                    })
                    .OrderByDescending(u => u.isOnline)
                    .ThenBy(u => u.username)
                    .ToList();

                return Json(new { success = true, users = userList });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Kullanıcılar yüklenemedi: " + ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetUserProfile(string username)
        {
            try
            {
                var user = await _userService.GetUserByUsernameAsync(username);
                if (user == null)
                {
                    return Json(new { success = false, message = "Kullanıcı bulunamadı" });
                }

                var userProfile = new
                {
                    username = user.Username,
                    displayName = user.DisplayName,
                    bio = user.Bio,
                    location = user.Location,
                    profileImageUrl = user.ProfileImageUrl,
                    isOnline = user.IsOnline,
                    userStatus = user.UserStatus,
                    lastActive = user.LastActive,
                    createdAt = user.CreatedAt
                };

                return Json(new { success = true, user = userProfile });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Profil yüklenemedi: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> ClearPrivateMessages([FromBody] ClearChatRequest request)
        {
            try
            {
                var currentUsername = User.Identity?.Name ?? "";
                if (string.IsNullOrEmpty(currentUsername))
                {
                    return Json(new { success = false, message = "Oturum süreniz dolmuş" });
                }

                // İki kullanıcı arasındaki tüm mesajları sil
                var messages = await _context.ChatMessages
                    .Where(m => (m.From == currentUsername && m.To == request.OtherUser) ||
                               (m.From == request.OtherUser && m.To == currentUsername))
                    .ToListAsync();

                _context.ChatMessages.RemoveRange(messages);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = $"{messages.Count} mesaj silindi" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Mesajlar silinemedi: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> UploadImage(IFormFile image)
        {
            try
            {
                if (image == null || image.Length == 0)
                {
                    return Json(new { success = false, message = "Dosya seçilmedi." });
                }

                // Validate file type
                var allowedTypes = new[] { "image/jpeg", "image/png", "image/gif", "image/webp" };
                if (!allowedTypes.Contains(image.ContentType.ToLower()))
                {
                    return Json(new { success = false, message = "Sadece resim dosyaları destekleniyor." });
                }

                // Validate file size (5MB max)
                if (image.Length > 5 * 1024 * 1024)
                {
                    return Json(new { success = false, message = "Dosya boyutu çok büyük. Maksimum 5MB olabilir." });
                }

                // Create upload directory if it doesn't exist
                var uploadsDirectory = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "chat-images");
                if (!Directory.Exists(uploadsDirectory))
                {
                    Directory.CreateDirectory(uploadsDirectory);
                }

                // Generate unique filename
                var fileExtension = Path.GetExtension(image.FileName);
                var fileName = $"{Guid.NewGuid()}{fileExtension}";
                var filePath = Path.Combine(uploadsDirectory, fileName);

                // Save file
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await image.CopyToAsync(stream);
                }

                // Return image URL
                var imageUrl = $"/uploads/chat-images/{fileName}";
                return Json(new { success = true, imageUrl = imageUrl });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Resim yüklenirken hata oluştu: " + ex.Message });
            }
        }

        // Group Management Endpoints
        [HttpGet]
        public async Task<IActionResult> CreateGroup()
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            if (userId == 0)
            {
                return RedirectToAction("Index", "Home");
            }

            // Kullanıcı bilgilerini ViewBag'e koy
            var currentUser = await _context.Users.FindAsync(userId);
            ViewBag.Username = currentUser?.Username;
            ViewBag.DisplayName = currentUser?.DisplayName;
            ViewBag.ProfileImageUrl = currentUser?.ProfileImageUrl;
            ViewBag.UserId = userId;
            ViewBag.Title = "Yeni Grup Oluştur";

            return View();
        }

        public class CreateGroupRequest
        {
            public string Name { get; set; } = string.Empty;
            public string? Description { get; set; }
            public string? GroupImageUrl { get; set; }
            public bool IsPrivate { get; set; }
            public int[] MemberIds { get; set; } = Array.Empty<int>();
        }

        [HttpPost]
        public async Task<IActionResult> CreateGroup([FromBody] CreateGroupRequest request)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            if (userId == 0)
            {
                return Json(new { success = false, message = "Oturum bilgisi bulunamadı" });
            }

            if (request == null || string.IsNullOrWhiteSpace(request.Name))
            {
                return Json(new { success = false, message = "Grup adı boş olamaz" });
            }

            // Normalize member list (remove duplicates, remove creator if accidentally present)
            var memberSet = new HashSet<int>(request.MemberIds ?? Array.Empty<int>());
            memberSet.Remove(userId); // creator already added inside service

            if (memberSet.Count < 1)
            {
                return Json(new { success = false, message = "En az 1 üye seçmelisiniz" });
            }
            if (memberSet.Count > 256)
            {
                return Json(new { success = false, message = "Üye sayısı 256'yı aşamaz" });
            }

            try
            {
                var group = await _groupService.CreateGroupAsync(
                    request.Name.Trim(),
                    request.Description?.Trim() ?? string.Empty,
                    request.GroupImageUrl,
                    userId,
                    memberSet.ToArray(),
                    request.IsPrivate
                );
                if (group != null)
                {
                    return Json(new { success = true, groupId = group.Id, message = "Grup başarıyla oluşturuldu" });
                }
                else
                {
                    return Json(new { success = false, message = "Grup oluşturulamadı" });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Hata: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> UploadGroupImage(IFormFile image)
        {
            if (image == null || image.Length == 0)
            {
                return Json(new { success = false, message = "Dosya alınamadı" });
            }
            try
            {
                // Geçici olarak wwwroot/uploads/groups içine kaydet
                var uploadsRoot = Path.Combine(_env.ContentRootPath, "wwwroot", "uploads", "groups");
                if (!Directory.Exists(uploadsRoot)) Directory.CreateDirectory(uploadsRoot);
                var fileName = Guid.NewGuid().ToString("N") + Path.GetExtension(image.FileName);
                var fullPath = Path.Combine(uploadsRoot, fileName);
                using (var fs = System.IO.File.Create(fullPath))
                {
                    await image.CopyToAsync(fs);
                }
                var relPath = "/uploads/groups/" + fileName;
                return Json(new { success = true, imageUrl = relPath });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Resim kaydedilemedi: " + ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> Group(int id)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            if (userId == 0)
            {
                return RedirectToAction("Index", "Home");
            }

            // Kullanıcının gruba üye olup olmadığını kontrol et
            var isMember = await _groupService.IsUserMemberAsync(id, userId);
            if (!isMember)
            {
                TempData["Error"] = "Bu gruba erişim yetkiniz yok";
                return RedirectToAction("Index");
            }

            var group = await _groupService.GetGroupByIdAsync(id);
            if (group == null)
            {
                TempData["Error"] = "Grup bulunamadı";
                return RedirectToAction("Index");
            }

            var isAdmin = await _groupService.IsUserAdminAsync(id, userId);
            var members = await _groupService.GetGroupMembersAsync(id);
            var messages = await _groupService.GetGroupMessagesAsync(id);

            ViewBag.Group = group!;
            ViewBag.IsAdmin = isAdmin;
            ViewBag.Members = members!;
            ViewBag.Messages = messages!;
            ViewBag.UserId = userId;
            ViewBag.Username = User.Identity?.Name ?? "";
            ViewBag.DisplayName = User.FindFirst("DisplayName")?.Value ?? User.Identity?.Name ?? "";
            ViewBag.Title = $"{group.Name} - Grup Sohbeti";

            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetMyGroups()
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            if (userId == 0)
            {
                return Json(new { success = false, message = "Oturum bilgisi bulunamadı" });
            }

            try
            {
                var groups = await _groupService.GetUserGroupsAsync(userId);
                var groupData = groups.Select(g => new
                {
                    id = g.Id,
                    name = g.Name,
                    description = g.Description,
                    groupImageUrl = g.GroupImageUrl,
                    memberCount = g.GroupMembers?.Count ?? 0,
                    lastActivityAt = g.LastActivityAt,
                    isPrivate = g.IsPrivate
                }).ToList();

                return Json(new { success = true, groups = groupData });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Hata: " + ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetGroupMembers(int groupId)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            if (userId == 0)
            {
                return Json(new { success = false, message = "Oturum bilgisi bulunamadı" });
            }

            // Kullanıcının gruba üye olup olmadığını kontrol et
            var isMember = await _groupService.IsUserMemberAsync(groupId, userId);
            if (!isMember)
            {
                return Json(new { success = false, message = "Bu gruba erişim yetkiniz yok" });
            }

            try
            {
                var members = await _groupService.GetGroupMembersAsync(groupId);
                var memberData = members.Select(m => new
                {
                    id = m.User.Id,
                    username = m.User.Username,
                    displayName = m.User.DisplayName,
                    profileImageUrl = m.User.ProfileImageUrl,
                    isAdmin = m.IsAdmin,
                    joinedAt = m.JoinedAt
                }).ToList();

                return Json(new { success = true, members = memberData });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Hata: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> AddGroupMember(int groupId, int memberId)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            if (userId == 0)
            {
                return Json(new { success = false, message = "Oturum bilgisi bulunamadı" });
            }

            try
            {
                var success = await _groupService.AddMemberAsync(groupId, memberId, userId);
                if (success)
                {
                    return Json(new { success = true, message = "Üye başarıyla eklendi" });
                }
                else
                {
                    return Json(new { success = false, message = "Üye eklenemedi" });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Hata: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> RemoveGroupMember(int groupId, int memberId)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            if (userId == 0)
            {
                return Json(new { success = false, message = "Oturum bilgisi bulunamadı" });
            }

            try
            {
                var success = await _groupService.RemoveMemberAsync(groupId, memberId, userId);
                if (success)
                {
                    return Json(new { success = true, message = "Üye başarıyla çıkarıldı" });
                }
                else
                {
                    return Json(new { success = false, message = "Üye çıkarılamadı" });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Hata: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> UpdateGroup(int groupId, string name, string description, string? groupImageUrl)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            if (userId == 0)
            {
                return Json(new { success = false, message = "Oturum bilgisi bulunamadı" });
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                return Json(new { success = false, message = "Grup adı boş olamaz" });
            }

            try
            {
                var success = await _groupService.UpdateGroupAsync(groupId, name, description ?? "", groupImageUrl, userId);
                if (success)
                {
                    return Json(new { success = true, message = "Grup bilgileri güncellendi" });
                }
                else
                {
                    return Json(new { success = false, message = "Grup güncellenemedi" });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Hata: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> SendGroupMessage(int groupId, string message, string messageType = "TEXT")
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            if (userId == 0)
            {
                return Json(new { success = false, message = "Oturum bilgisi bulunamadı" });
            }

            // Kullanıcının gruba üye olup olmadığını kontrol et
            var isMember = await _groupService.IsUserMemberAsync(groupId, userId);
            if (!isMember)
            {
                return Json(new { success = false, message = "Bu gruba mesaj gönderme yetkiniz yok" });
            }

            try
            {
                var groupMessage = await _groupService.AddGroupMessageAsync(groupId, userId, message, messageType);
                if (groupMessage != null)
                {
                    return Json(new { 
                        success = true, 
                        messageId = groupMessage.Id,
                        message = "Mesaj gönderildi"
                    });
                }
                else
                {
                    return Json(new { success = false, message = "Mesaj gönderilemedi" });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Hata: " + ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> SearchUsers(string query)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            if (userId == 0)
            {
                return Json(new { success = false, message = "Oturum bilgisi bulunamadı" });
            }

            if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
            {
                return Json(new { success = false, message = "Arama terimi en az 2 karakter olmalıdır" });
            }

            try
            {
                var users = await _context.Users
                    .Where(u => u.Id != userId && 
                               (u.Username.ToLower().Contains(query.ToLower()) || 
                                (u.DisplayName != null && u.DisplayName.ToLower().Contains(query.ToLower()))))
                    .Select(u => new { 
                        u.Id, 
                        u.Username, 
                        u.DisplayName, 
                        u.ProfileImageUrl 
                    })
                    .Take(20)
                    .ToListAsync();

                return Json(new { success = true, users = users });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Arama hatası: " + ex.Message });
            }
        }
    }

    
}

public class ClearChatRequest
{
    public string OtherUser { get; set; } = string.Empty;
}
