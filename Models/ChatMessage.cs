namespace ChatApp.Models;

using System.ComponentModel.DataAnnotations;

public class ChatMessage
{
    public int Id { get; set; }
    
    // Database fields - User IDs
    public int FromUserId { get; set; }
    public int? ToUserId { get; set; } // Null for public messages
    
    // Legacy fields for API compatibility
    [Required]
    [StringLength(50)]
    public string From { get; set; } = string.Empty;
    
    [StringLength(50)]
    public string To { get; set; } = string.Empty;
    
    [Required]
    [StringLength(1000)]
    public string Message { get; set; } = string.Empty;
    
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    [StringLength(20)]
    public string MessageType { get; set; } = "private"; // "private", "public", "system"
    
    public bool IsRead { get; set; } = false;

    // Navigation properties (disabled for now)
    // public virtual User? FromUser { get; set; }
    // public virtual User? ToUser { get; set; }

    // Parametresiz constructor (Model binding için gerekli)
    public ChatMessage()
    {
    }

    // Constructor with parameters
    public ChatMessage(string from, string to, string message)
    {
        From = from;
        To = to;
        Message = message;
        Timestamp = DateTime.UtcNow;
    }
}

// Register Request Model
public class RegisterRequest
{
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? ProfileImageUrl { get; set; }
}