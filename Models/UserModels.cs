namespace ChatApp.Models;

using System.ComponentModel.DataAnnotations;

public class LoginRequest
{
    [Required]
    [StringLength(50, MinimumLength = 3)]
    public string Username { get; set; } = string.Empty;
    
    [Required]
    [StringLength(100, MinimumLength = 6)]
    public string Password { get; set; } = string.Empty;
    
    public string? Email { get; set; }
    public string? DisplayName { get; set; }
    public string? ConnectionId { get; set; }
    public string? Avatar { get; set; }
}

public class LogoutRequest
{
    [Required]
    public string Username { get; set; } = string.Empty;
    public int UserId { get; set; }
}

public class UserSession
{
    public int Id { get; set; }
    
    [Required]
    public int UserId { get; set; }
    
    public DateTime LoginTime { get; set; } = DateTime.UtcNow;
    
    public DateTime? LogoutTime { get; set; }
    
    public bool IsOnline { get; set; } = true;
    
    [StringLength(100)]
    public string? ConnectionId { get; set; }
    
    [StringLength(45)] // IP address length
    public string? IpAddress { get; set; }
    
    [StringLength(500)] // User agent
    public string? UserAgent { get; set; }

    // Navigation property (disabled for now)
    // public virtual User? User { get; set; }
}
