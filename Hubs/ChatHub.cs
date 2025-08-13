namespace ChatApp.Hubs;

using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;
using ChatApp.Models;
using ChatApp.Data;
using ChatApp.Services;
using ChatApp.Constants;
using System.Threading.Tasks;

// [Authorize] // Geçici olarak kaldırıyoruz debug için
[Authorize]
public class ChatHub : Hub
{
    private readonly ChatDbContext _context;
    private readonly IUserService _userService;
    private readonly IEncryptionService _encryptionService;
    private readonly IGroupService _groupService;

    public ChatHub(ChatDbContext context, IUserService userService, IEncryptionService encryptionService, IGroupService groupService)
    {
        _context = context;
        _userService = userService;
        _encryptionService = encryptionService;
        _groupService = groupService;
    }

    // Kullanıcı belirli bir gruba katılır (kendi username'i ile)
    public async Task JoinUserGroup(string username)
    {
        try
        {
            if (string.IsNullOrEmpty(username))
            {
                await Clients.Caller.SendAsync("MessageError", "Geçersiz kullanıcı adı");
                return;
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, username);
            await Clients.Group(username).SendAsync("UserJoined", $"🟢 {username} chat'e katıldı");
            Console.WriteLine($"👤 {username} joined group with connection {Context.ConnectionId}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error in JoinUserGroup: {ex.Message}");
            await Clients.Caller.SendAsync("MessageError", "Gruba katılma hatası: " + ex.Message);
        }
    }

    // Kullanıcı gruptan ayrılır
    public async Task LeaveUserGroup(string username)
    {
        try
        {
            if (string.IsNullOrEmpty(username))
            {
                return;
            }

            await Groups.RemoveFromGroupAsync(Context.ConnectionId, username);
            await Clients.Group(username).SendAsync("UserLeft", $"🔴 {username} chat'ten ayrıldı");
            Console.WriteLine($"👤 {username} left group");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error in LeaveUserGroup: {ex.Message}");
        }
    }

    // Ana mesaj gönderme metodu - veritabanına kaydet
    public async Task SendMessage(string from, string to, string message)
    {
        try
        {
            Console.WriteLine($"🔍 SendMessage called: from={from}, to={to}, message length={message?.Length}");
            
            // Input validation
            if (string.IsNullOrEmpty(from) || string.IsNullOrEmpty(to) || string.IsNullOrEmpty(message))
            {
                Console.WriteLine($"❌ SendMessage validation failed: from={from}, to={to}, message empty={string.IsNullOrEmpty(message)}");
                await Clients.Caller.SendAsync("MessageError", "Mesaj bilgileri eksik");
                return;
            }

            if (message.Length > 500)
            {
                Console.WriteLine($"❌ Message too long: {message.Length} chars");
                await Clients.Caller.SendAsync("MessageError", "Mesaj çok uzun (maksimum 500 karakter)");
                return;
            }

            // Kullanıcıları bul
            Console.WriteLine($"🔍 Looking for users: from={from}, to={to}");
            var fromUser = await _userService.GetUserByUsernameAsync(from);
            var toUser = await _userService.GetUserByUsernameAsync(to);

            if (fromUser == null)
            {
                Console.WriteLine($"❌ From user not found: {from}");
                await Clients.Caller.SendAsync("MessageError", "Gönderici kullanıcı bulunamadı");
                return;
            }

            if (toUser == null)
            {
                Console.WriteLine($"❌ To user not found: {to}");
                await Clients.Caller.SendAsync("MessageError", "Hedef kullanıcı bulunamadı");
                return;
            }

            Console.WriteLine($"✅ Users found: fromUser.Id={fromUser.Id}, toUser.Id={toUser.Id}");

            // Mesajı şifrele
            var encryptedMessage = _encryptionService.Encrypt(message);
            Console.WriteLine($"🔐 Message encrypted, length: {encryptedMessage.Length}");

            // Veritabanına kaydet (şifrelenmiş mesaj)
            var chatMessage = new ChatMessage
            {
                FromUserId = fromUser.Id,
                ToUserId = toUser.Id,
                From = from,
                To = to,
                Message = encryptedMessage, // Şifrelenmiş mesaj
                Timestamp = DateTime.UtcNow,
                MessageType = MessageTypes.PRIVATE,
                IsRead = false
            };

            Console.WriteLine($"🔍 Adding message to database...");
            _context.ChatMessages.Add(chatMessage);
            
            Console.WriteLine($"🔍 Saving changes to database...");
            var savedCount = await _context.SaveChangesAsync();
            Console.WriteLine($"✅ Database save completed. Rows affected: {savedCount}");

            // SignalR ile anlık gönderim (plain text - sadece canlı görüntüleme için)
            await Clients.Group(to).SendAsync("ReceiveMessage", new {
                From = from,
                To = to,
                Message = message, // Plain text - sadece anlık görüntüleme
                Timestamp = chatMessage.Timestamp,
                MessageType = "Private",
                ProfileImageUrl = fromUser.ProfileImageUrl
            });
            
            // Gönderene de konfirmasyon gönder
            await Clients.Group(from).SendAsync("MessageSent", new {
                From = from,
                To = to,
                Message = message,
                Timestamp = chatMessage.Timestamp,
                MessageType = "Private",
                ProfileImageUrl = fromUser.ProfileImageUrl
            });
            
            Console.WriteLine($"📤 Private message saved and sent: {from} → {to} (Message ID: {chatMessage.Id})");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error in SendMessage: {ex.Message}");
            Console.WriteLine($"❌ Stack trace: {ex.StackTrace}");
            await Clients.Caller.SendAsync("MessageError", "Mesaj gönderilemedi: " + ex.Message);
        }
    }

    // Eski metodla uyumluluk: frontend artık SendMessage kullanıyor; yine de geriye dönük çağrılar için delegasyon
    public async Task SendPrivateMessage(string toUsername, string message, string? gifUrl = null)
    {
        Console.WriteLine($"↪️ Delegating SendPrivateMessage -> SendMessage: to={toUsername}");
        var fromUsername = Context.User?.Identity?.Name;
        if (string.IsNullOrEmpty(fromUsername))
        {
            fromUsername = Context.User?.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value;
        }
        if (string.IsNullOrEmpty(fromUsername))
        {
            var userIdClaim = Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(userIdClaim) && int.TryParse(userIdClaim, out int userId))
            {
                var user = await _userService.GetUserByIdAsync(userId);
                fromUsername = user?.Username;
            }
        }
        if (string.IsNullOrEmpty(fromUsername))
        {
            await Clients.Caller.SendAsync("MessageError", "Oturum bulunamadı");
            return;
        }
        await SendMessage(fromUsername, toUsername, message);
    }

    // Typing indicator metotları
    public async Task StartTyping(string username, string targetUser)
    {
        try
        {
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(targetUser))
            {
                return;
            }

            // Sadece hedef kullanıcıya yazıyor durumunu gönder
            await Clients.Group(targetUser).SendAsync("UserStartedTyping", username);
            Console.WriteLine($"✍️ {username} started typing to {targetUser}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error in StartTyping: {ex.Message}");
        }
    }

    public async Task StopTyping(string username, string targetUser)
    {
        try
        {
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(targetUser))
            {
                return;
            }

            // Sadece hedef kullanıcıya yazma durumunun bittiğini gönder
            await Clients.Group(targetUser).SendAsync("UserStoppedTyping", username);
            Console.WriteLine($"✋ {username} stopped typing to {targetUser}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error in StopTyping: {ex.Message}");
        }
    }

    // Broadcast mesaj gönderme (tüm kullanıcılara)
    public async Task SendBroadcastMessage(string from, string message)
    {
        try
        {
            // Input validation
            if (string.IsNullOrEmpty(from) || string.IsNullOrEmpty(message))
            {
                await Clients.Caller.SendAsync("MessageError", "Mesaj bilgileri eksik");
                return;
            }

            if (message.Length > 500)
            {
                await Clients.Caller.SendAsync("MessageError", "Mesaj çok uzun (maksimum 500 karakter)");
                return;
            }

            // Kullanıcıyı bul
            var fromUser = await _userService.GetUserByUsernameAsync(from);

            if (fromUser == null)
            {
                await Clients.Caller.SendAsync("MessageError", "Gönderici kullanıcı bulunamadı");
                return;
            }

            // Mesajı şifrele
            var encryptedMessage = _encryptionService.Encrypt(message);

            // Veritabanına kaydet (şifrelenmiş mesaj) - Genel Chat için sistem kullanıcısı
            var chatMessage = new ChatMessage
            {
                FromUserId = fromUser.Id,
                ToUserId = SystemUsers.GENERAL_CHAT_USER_ID, // Genel chat sistem kullanıcısı
                From = from,
                To = SystemUsers.GENERAL_CHAT_USERNAME, // Sistem kullanıcısı username
                Message = encryptedMessage, // Şifrelenmiş mesaj
                Timestamp = DateTime.UtcNow,
                MessageType = MessageTypes.PUBLIC,
                IsRead = true // Public mesajlar direkt okunmuş sayılır
            };

            _context.ChatMessages.Add(chatMessage);
            await _context.SaveChangesAsync();

            // Tüm kullanıcılara gönder (broadcast)
            await Clients.All.SendAsync("ReceiveBroadcast", from, message, chatMessage.Timestamp, fromUser.ProfileImageUrl);
            Console.WriteLine($"📢 Broadcast message saved and sent from {from}: {message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error in SendBroadcastMessage: {ex.Message}");
            await Clients.Caller.SendAsync("MessageError", "Mesaj gönderilemedi: " + ex.Message);
        }
    }

    // Mesajı okundu olarak işaretle
    public async Task MarkAsRead(int messageId, string username)
    {
        try
        {
            var message = await _context.ChatMessages.FindAsync(messageId);
            if (message != null && message.To == username)
            {
                message.IsRead = true;
                await _context.SaveChangesAsync();
                
                // Gönderen kullanıcıya mesajın okunduğunu bildir
                await Clients.Group(message.From).SendAsync("MessageRead", messageId, username);
                Console.WriteLine($"✅ Message {messageId} marked as read by {username}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error in MarkAsRead: {ex.Message}");
        }
    }

    public override async Task OnConnectedAsync()
    {
        var username = Context.User?.Identity?.Name;
        if (!string.IsNullOrEmpty(username))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, username);
            Console.WriteLine($"🔗 New connection: {Context.ConnectionId} - User: {username} auto-joined group");
        }
        else
        {
            Console.WriteLine($"🔗 New connection: {Context.ConnectionId} - No username found");
        }
        
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var username = Context.User?.Identity?.Name;
        if (!string.IsNullOrEmpty(username))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, username);
            Console.WriteLine($"🔌 Disconnected: {Context.ConnectionId} - User: {username} left group");
        }
        else
        {
            Console.WriteLine($"🔌 Disconnected: {Context.ConnectionId}");
        }
        
        await base.OnDisconnectedAsync(exception);
    }

    // Helper method to get current user ID
    private int GetCurrentUserId()
    {
        var userIdClaim = Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(userIdClaim) && int.TryParse(userIdClaim, out int userId))
        {
            return userId;
        }
        return 0;
    }

    // Group Chat Methods
    public async Task JoinGroup(int groupId)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == 0)
            {
                await Clients.Caller.SendAsync("GroupError", "Oturum bilgisi bulunamadı");
                return;
            }

            // Kullanıcının gruba üye olup olmadığını kontrol et
            var isMember = await _groupService.IsUserMemberAsync(groupId, userId);
            if (!isMember)
            {
                await Clients.Caller.SendAsync("GroupError", "Bu gruba erişim yetkiniz yok");
                return;
            }

            var groupName = $"Group_{groupId}";
            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
            
            var user = await _userService.GetUserByIdAsync(userId);
            var username = user?.Username ?? "Bilinmeyen Kullanıcı";
            
            await Clients.Group(groupName).SendAsync("GroupUserJoined", new
            {
                userId = userId,
                username = username,
                message = $"🟢 {username} gruba katıldı"
            });

            Console.WriteLine($"👥 User {username} joined group {groupId} with connection {Context.ConnectionId}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error in JoinGroup: {ex.Message}");
            await Clients.Caller.SendAsync("GroupError", "Gruba katılma hatası: " + ex.Message);
        }
    }

    public async Task LeaveGroup(int groupId)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == 0) return;

            var groupName = $"Group_{groupId}";
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
            
            var user = await _userService.GetUserByIdAsync(userId);
            var username = user?.Username ?? "Bilinmeyen Kullanıcı";
            
            await Clients.Group(groupName).SendAsync("GroupUserLeft", new
            {
                userId = userId,
                username = username,
                message = $"🔴 {username} gruptan ayrıldı"
            });

            Console.WriteLine($"👥 User {username} left group {groupId}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error in LeaveGroup: {ex.Message}");
        }
    }

    public async Task SendGroupMessage(int groupId, string message, string messageType = "TEXT")
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == 0)
            {
                await Clients.Caller.SendAsync("GroupError", "Oturum bilgisi bulunamadı");
                return;
            }

            // Kullanıcının gruba üye olup olmadığını kontrol et
            var isMember = await _groupService.IsUserMemberAsync(groupId, userId);
            if (!isMember)
            {
                await Clients.Caller.SendAsync("GroupError", "Bu gruba mesaj gönderme yetkiniz yok");
                return;
            }

            // Mesajı veritabanına kaydet
            var groupMessage = await _groupService.AddGroupMessageAsync(groupId, userId, message, messageType);
            if (groupMessage == null)
            {
                await Clients.Caller.SendAsync("GroupError", "Mesaj kaydedilemedi");
                return;
            }

            var user = await _userService.GetUserByIdAsync(userId);
            var groupName = $"Group_{groupId}";

            // Gruba mesajı gönder
            await Clients.Group(groupName).SendAsync("ReceiveGroupMessage", new
            {
                messageId = groupMessage.Id,
                groupId = groupId,
                fromUserId = userId,
                fromUsername = user?.Username ?? "Bilinmeyen",
                fromDisplayName = user?.DisplayName ?? user?.Username ?? "Bilinmeyen",
                fromProfileImageUrl = user?.ProfileImageUrl,
                message = message,
                messageType = messageType,
                sentAt = groupMessage.Timestamp,
                isEdited = false
            });

            Console.WriteLine($"💬 Group message sent in group {groupId} by user {userId}: {message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error in SendGroupMessage: {ex.Message}");
            await Clients.Caller.SendAsync("GroupError", "Mesaj gönderme hatası: " + ex.Message);
        }
    }

    public async Task NotifyGroupMemberAdded(int groupId, int newMemberId)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == 0) return;

            // Sadece adminler üye ekleyebilir, bu kontrol zaten service'de yapılıyor
            var isAdmin = await _groupService.IsUserAdminAsync(groupId, userId);
            if (!isAdmin) return;

            var newMember = await _userService.GetUserByIdAsync(newMemberId);
            var groupName = $"Group_{groupId}";

            await Clients.Group(groupName).SendAsync("GroupMemberAdded", new
            {
                groupId = groupId,
                memberId = newMemberId,
                memberUsername = newMember?.Username ?? "Bilinmeyen",
                memberDisplayName = newMember?.DisplayName ?? newMember?.Username ?? "Bilinmeyen",
                memberProfileImageUrl = newMember?.ProfileImageUrl,
                addedBy = userId
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error in NotifyGroupMemberAdded: {ex.Message}");
        }
    }

    public async Task NotifyGroupMemberRemoved(int groupId, int removedMemberId)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == 0) return;

            var removedMember = await _userService.GetUserByIdAsync(removedMemberId);
            var groupName = $"Group_{groupId}";

            await Clients.Group(groupName).SendAsync("GroupMemberRemoved", new
            {
                groupId = groupId,
                memberId = removedMemberId,
                memberUsername = removedMember?.Username ?? "Bilinmeyen",
                removedBy = userId
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error in NotifyGroupMemberRemoved: {ex.Message}");
        }
    }

    public async Task NotifyGroupUpdated(int groupId, string newName, string newDescription)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == 0) return;

            var groupName = $"Group_{groupId}";

            await Clients.Group(groupName).SendAsync("GroupUpdated", new
            {
                groupId = groupId,
                newName = newName,
                newDescription = newDescription,
                updatedBy = userId
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error in NotifyGroupUpdated: {ex.Message}");
        }
    }
}