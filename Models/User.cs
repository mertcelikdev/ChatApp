using System.ComponentModel.DataAnnotations;

namespace ChatApp.Models;

public static class UserStatusOptions
{
    public const string Online = "Online";
    public const string Offline = "Offline";
    public const string Busy = "Busy";
    public const string Away = "Away";
    
    public static readonly string[] AllStatuses = { Online, Offline, Busy, Away };
}

public class User
{
    public int Id { get; set; }
    
    [Required]
    [StringLength(50, MinimumLength = 3)]
    public string Username { get; set; } = string.Empty;
    
    [Required]
    [StringLength(255)] // Hash için yeterli alan
    public string PasswordHash { get; set; } = string.Empty;
    
    [EmailAddress]
    [StringLength(100)]
    public string? Email { get; set; }
    
    [StringLength(100)]
    public string? DisplayName { get; set; }
    
    [StringLength(500)] // Bio için yeterli alan
    public string? Bio { get; set; }
    
    [StringLength(100)]
    public string? Location { get; set; }

    // Sadece gereksiz alanları kaldırdık: Country, CountryCode, City
    // Database ID'leri yeterli
    public int? CountryId { get; set; }
    public int? CityId { get; set; }
    
    [StringLength(255)]
    public string? ProfileImageUrl { get; set; }
    
    [StringLength(20)]
    public string UserStatus { get; set; } = UserStatusOptions.Online;
    
    public bool IsOnline { get; set; } = false;
    
    public DateTime? LastActive { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime? LastLoginAt { get; set; }
    
    // Navigation properties (disabled for now)
    // public virtual ICollection<ChatMessage> SentMessages { get; set; } = new List<ChatMessage>();
    // public virtual ICollection<ChatMessage> ReceivedMessages { get; set; } = new List<ChatMessage>();
    // public virtual ICollection<UserSession> Sessions { get; set; } = new List<UserSession>();
}

public class BlockedUser
{
    public int Id { get; set; }
    
    [Required]
    public int UserId { get; set; } // Engelleyen kullanıcı
    
    [Required]
    public int BlockedUserId { get; set; } // Engellenen kullanıcı
    
    public DateTime BlockedAt { get; set; } = DateTime.UtcNow;
    
    public string? Reason { get; set; }
    
    // Navigation properties geçici olarak kapalı
    // public virtual User? User { get; set; }
    // public virtual User? BlockedUserNavigation { get; set; }
}

public class UserReport
{
    public int Id { get; set; }
    
    [Required]
    public int ReporterId { get; set; } // Şikayetçi kullanıcı
    
    [Required]
    public int ReportedUserId { get; set; } // Şikayet edilen kullanıcı
    
    [Required]
    [StringLength(500)]
    public string Reason { get; set; } = string.Empty;
    
    [StringLength(50)]
    public string Category { get; set; } = "General"; // "Spam", "Harassment", "Inappropriate", "General"
    
    public DateTime ReportedAt { get; set; } = DateTime.UtcNow;
    
    public bool IsResolved { get; set; } = false;
    
    public string? AdminNotes { get; set; }
    
    public DateTime? ResolvedAt { get; set; }
    
    // Navigation properties geçici olarak kapalı
    // public virtual User? Reporter { get; set; }
    // public virtual User? ReportedUser { get; set; }
}
