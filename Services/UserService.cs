using Microsoft.EntityFrameworkCore;
using ChatApp.Data;
using ChatApp.Models;
using ChatApp.Constants;

namespace ChatApp.Services;

public interface IUserService
{
    Task<User?> GetUserByUsernameAsync(string username);
    Task<User?> ValidateUserAsync(string username, string password);
    Task<User?> AuthenticateUserAsync(string username, string password); // Login için
    Task<User> CreateUserAsync(string username, string password, string? profileImageUrl = null);
    Task<User> UpdateUserAsync(User user);
    Task UpdateLastLoginAsync(int userId); // Son giriş tarihini güncelle
    Task UpdateLastActiveAsync(int userId); // Son aktivite tarihini güncelle
    Task SetUserOnlineAsync(int userId); // Kullanıcıyı online yap
    Task SetUserOfflineAsync(int userId); // Kullanıcıyı offline yap
    Task<UserSession> CreateSessionAsync(int userId, string? connectionId = null);
    Task EndSessionAsync(int userId);
    Task<IEnumerable<object>> GetActiveUsersAsync();
    Task<IEnumerable<UserSession>> GetActiveSessionsAsync(int userId);
    Task<IEnumerable<object>> GetPublicMessagesAsync(int count = 50);
    Task<IEnumerable<object>> GetPrivateMessagesAsync(int userId, string? withUser = null, int count = 50);
    Task SavePublicMessageAsync(int userId, string message);
    Task SavePrivateMessageAsync(int fromUserId, int toUserId, string message);
    Task<bool> IsUserOnlineAsync(string username);
    Task ForceLogoutUserAsync(string username); // Zorla logout için
    Task<string> DebugOfflineAllUsersAsync(); // Debug - tüm kullanıcıları offline yap
    Task<IEnumerable<User>> GetAllUsersAsync(); // Tüm kullanıcıları getir
    Task<User?> GetUserByIdAsync(int id); // ID ile kullanıcı getir
    Task DeleteUserAsync(int id); // Kullanıcı sil (Admin)
}

public class UserService : IUserService
{
    private readonly ChatDbContext _context;

    public UserService(ChatDbContext context)
    {
        _context = context;
    }

    public async Task<User?> GetUserByUsernameAsync(string username)
    {
        return await _context.Users
            .FirstOrDefaultAsync(u => u.Username == username);
    }

    public async Task<User?> GetUserByIdAsync(int id)
    {
        return await _context.Users
            .FirstOrDefaultAsync(u => u.Id == id);
    }

    public async Task<User?> ValidateUserAsync(string username, string password)
    {
        var user = await GetUserByUsernameAsync(username);
        if (user == null) return null;

        // BCrypt ile şifre doğrulama
        var isValid = BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);
        return isValid ? user : null;
    }

    public async Task<User> CreateUserAsync(string username, string password, string? profileImageUrl = null)
    {
        var user = new User
        {
            Username = username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            ProfileImageUrl = profileImageUrl,
            DisplayName = username,
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow,
            IsOnline = true
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return user;
    }

    public async Task<User> UpdateUserAsync(User user)
    {
        // DateTime UTC normalizasyonu
        user.CreatedAt = DateTime.SpecifyKind(user.CreatedAt, DateTimeKind.Utc);
        if (user.LastActive.HasValue)
        {
            if (user.LastActive.Value.Kind == DateTimeKind.Local)
                user.LastActive = user.LastActive.Value.ToUniversalTime();
            else if (user.LastActive.Value.Kind == DateTimeKind.Unspecified)
                user.LastActive = DateTime.SpecifyKind(user.LastActive.Value, DateTimeKind.Utc);
        }
        if (user.LastLoginAt.HasValue)
        {
            if (user.LastLoginAt.Value.Kind == DateTimeKind.Local)
                user.LastLoginAt = user.LastLoginAt.Value.ToUniversalTime();
            else if (user.LastLoginAt.Value.Kind == DateTimeKind.Unspecified)
                user.LastLoginAt = DateTime.SpecifyKind(user.LastLoginAt.Value, DateTimeKind.Utc);
        }
        _context.Users.Update(user);
        await _context.SaveChangesAsync();
        return user;
    }

    public async Task<UserSession> CreateSessionAsync(int userId, string? connectionId = null)
    {
        // Önce kullanıcının eski aktif oturumlarını kapat
        var existingSessions = await _context.UserSessions
            .Where(s => s.UserId == userId && s.IsOnline)
            .ToListAsync();

        foreach (var session in existingSessions)
        {
            session.IsOnline = false;
            session.LogoutTime = DateTime.UtcNow;
        }

        // Kullanıcıyı online yap
        var user = await _context.Users.FindAsync(userId);
        if (user != null)
        {
            user.IsOnline = true;
            user.LastLoginAt = DateTime.UtcNow;
        }

        // Yeni oturum oluştur
        var newSession = new UserSession
        {
            UserId = userId,
            LoginTime = DateTime.UtcNow,
            IsOnline = true,
            ConnectionId = connectionId
        };

        _context.UserSessions.Add(newSession);
        await _context.SaveChangesAsync();
        return newSession;
    }

    public async Task EndSessionAsync(int userId)
    {
        var activeSessions = await _context.UserSessions
            .Where(s => s.UserId == userId && s.IsOnline)
            .ToListAsync();

        foreach (var session in activeSessions)
        {
            session.IsOnline = false;
            session.LogoutTime = DateTime.UtcNow;
        }

        // Kullanıcıyı offline yap
        var user = await _context.Users.FindAsync(userId);
        if (user != null)
        {
            user.IsOnline = false;
            user.LastActive = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
    }

    public async Task<IEnumerable<object>> GetActiveUsersAsync()
    {
        return await _context.Users
            .Where(u => u.IsOnline )
            .Select(u => new {
                id = u.Id,
                username = u.Username,
                profileImageUrl = u.ProfileImageUrl,
                displayName = u.DisplayName,
                lastActive = u.LastActive,
                isOnline = u.IsOnline
            })
            .ToListAsync();
    }

    public async Task<IEnumerable<UserSession>> GetActiveSessionsAsync(int userId)
    {
        return await _context.UserSessions
            .Where(s => s.UserId == userId && s.IsOnline)
            .ToListAsync();
    }

    public async Task<IEnumerable<object>> GetPublicMessagesAsync(int count = 50)
    {
        var messages = await _context.ChatMessages
            .Where(m => m.MessageType == "public")
            .OrderByDescending(m => m.Timestamp)
            .Take(count)
            .OrderBy(m => m.Timestamp)
            .ToListAsync();

        return messages.Select(m => new {
            id = m.Id,
            from = m.From,
            to = m.To,
            message = m.Message,
            timestamp = m.Timestamp,
            messageType = m.MessageType,
            isRead = m.IsRead
        });
    }

    public async Task<IEnumerable<object>> GetPrivateMessagesAsync(int userId, string? withUser = null, int count = 50)
    {
        var query = _context.ChatMessages
            .Where(m => m.MessageType == "private" && 
                       (m.FromUserId == userId || m.ToUserId == userId));

        if (!string.IsNullOrEmpty(withUser))
        {
            // Username ile filtreleme için From/To alanlarını kullan
            query = query.Where(m => 
                m.From == withUser || m.To == withUser);
        }

        var messages = await query
            .OrderByDescending(m => m.Timestamp)
            .Take(count)
            .OrderBy(m => m.Timestamp)
            .ToListAsync();

        return messages.Select(m => new {
            id = m.Id,
            from = m.From,
            to = m.To,
            message = m.Message,
            timestamp = m.Timestamp,
            messageType = m.MessageType,
            isRead = m.IsRead
        });
    }

    public async Task SavePublicMessageAsync(int userId, string message)
    {
        var chatMessage = new ChatMessage
        {
            FromUserId = userId,
            Message = message,
            MessageType = "public",
            Timestamp = DateTime.UtcNow,
            IsRead = false
        };

        _context.ChatMessages.Add(chatMessage);
        await _context.SaveChangesAsync();
    }

    public async Task SavePrivateMessageAsync(int fromUserId, int toUserId, string message)
    {
        var chatMessage = new ChatMessage
        {
            FromUserId = fromUserId,
            ToUserId = toUserId,
            Message = message,
            MessageType = "private",
            Timestamp = DateTime.UtcNow,
            IsRead = false
        };

        _context.ChatMessages.Add(chatMessage);
        await _context.SaveChangesAsync();
    }

    public async Task<bool> IsUserOnlineAsync(string username)
    {
        var user = await GetUserByUsernameAsync(username);
        return user?.IsOnline ?? false;
    }

    public async Task ForceLogoutUserAsync(string username)
    {
        var user = await GetUserByUsernameAsync(username);
        if (user != null)
        {
            // Kullanıcıyı offline yap
            user.IsOnline = false;
            user.UserStatus = UserStatusOptions.Offline;
            user.LastActive = DateTime.UtcNow;
            
            // Tüm aktif session'larını kapat
            var activeSessions = await _context.UserSessions
                .Where(s => s.UserId == user.Id && s.IsOnline)
                .ToListAsync();
                
            foreach (var session in activeSessions)
            {
                session.IsOnline = false;
                session.LogoutTime = DateTime.UtcNow;
            }
            
            await _context.SaveChangesAsync();
            Console.WriteLine($"🔴 Force logout: {username} - All sessions ended");
        }
    }

    public async Task<string> DebugOfflineAllUsersAsync()
    {
        var allUsers = await _context.Users.ToListAsync();
        var offlineCount = 0;
        
        foreach (var user in allUsers)
        {
            if (user.IsOnline)
            {
                user.IsOnline = false;
                user.UserStatus = UserStatusOptions.Offline;
                user.LastActive = DateTime.UtcNow;
                offlineCount++;
            }
        }
        
        var allSessions = await _context.UserSessions.Where(s => s.IsOnline).ToListAsync();
        foreach (var session in allSessions)
        {
            session.IsOnline = false;
            session.LogoutTime = DateTime.UtcNow;
        }
        
        await _context.SaveChangesAsync();
        
        var message = $"Tüm kullanıcılar offline yapıldı. {offlineCount} kullanıcı offline, {allSessions.Count} session kapatıldı.";
        Console.WriteLine($"🔴 Debug offline all: {message}");
        return message;
    }

    public async Task<IEnumerable<User>> GetAllUsersAsync()
    {
        return await _context.Users
            .Where(u =>  u.Id != SystemUsers.GENERAL_CHAT_USER_ID) // Sistem kullanıcısını hariç tut
            .OrderBy(u => u.Username)
            .ToListAsync();
    }

    // Authentication metodları
    public async Task<User?> AuthenticateUserAsync(string username, string password)
    {
        var user = await GetUserByUsernameAsync(username);
        if (user == null) return null;

        var isValid = BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);
        if (isValid)
        {
            await SetUserOnlineAsync(user.Id);
            return user;
        }
        return null;
    }

    public async Task UpdateLastLoginAsync(int userId)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user != null)
        {
            user.LastLoginAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }

    public async Task UpdateLastActiveAsync(int userId)
    {
        try
        {
            var user = await _context.Users.FindAsync(userId);
            if (user != null)
            {
                user.LastActive = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            // Silent fail - don't break user experience for activity tracking
            Console.WriteLine($"⚠️ Warning: Failed to update LastActive for user {userId}: {ex.Message}");
        }
    }

    public async Task SetUserOnlineAsync(int userId)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user != null)
        {
            user.IsOnline = true;
            // O an önceden Busy/Away seçilmişse onu koru; Offline ise Online'a çek
            if (user.UserStatus == UserStatusOptions.Offline)
                user.UserStatus = UserStatusOptions.Online;
            user.LastActive = DateTime.UtcNow;
            user.LastLoginAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }

    public async Task SetUserOfflineAsync(int userId)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user != null)
        {
            user.IsOnline = false;
            user.UserStatus = UserStatusOptions.Offline;
            user.LastActive = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }

    public async Task DeleteUserAsync(int id)
    {
        var user = await GetUserByIdAsync(id);
        if (user != null)
        {
            // Kullan�c� silindi
            user.IsOnline = false;
            await _context.SaveChangesAsync();
        }
    }
}
