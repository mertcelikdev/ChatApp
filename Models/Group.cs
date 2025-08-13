using System.ComponentModel.DataAnnotations;

namespace ChatApp.Models
{
    public class Group
    {
        public int Id { get; set; }
        
        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;
        
        [StringLength(500)]
        public string Description { get; set; } = string.Empty;
        
        public string? GroupImageUrl { get; set; }
        
        public int CreatedByUserId { get; set; }
        public User CreatedByUser { get; set; } = null!;
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;
        
        public bool IsActive { get; set; } = true;
        public bool IsPrivate { get; set; } = false;
        
        // Navigation properties
        public virtual ICollection<GroupMember> GroupMembers { get; set; } = new List<GroupMember>();
        public virtual ICollection<GroupMessage> Messages { get; set; } = new List<GroupMessage>();
    }
    
    public class GroupMember
    {
        public int Id { get; set; }
        
        public int GroupId { get; set; }
        public Group Group { get; set; } = null!;
        
        public int UserId { get; set; }
        public User User { get; set; } = null!;
        
        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
        
        public bool IsAdmin { get; set; } = false;
        
        public bool IsActive { get; set; } = true;
    }
    
    public class GroupMessage
    {
        public int Id { get; set; }
        
        public int GroupId { get; set; }
        public Group Group { get; set; } = null!;
        
        public int FromUserId { get; set; }
        public User FromUser { get; set; } = null!;
        
        [Required]
        public string Message { get; set; } = string.Empty;
        
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        
        public string MessageType { get; set; } = "TEXT"; // TEXT, IMAGE, FILE
        
        public bool IsDeleted { get; set; } = false;
        
        public DateTime? EditedAt { get; set; }
        
        public int EditCount { get; set; } = 0;
    }
}
