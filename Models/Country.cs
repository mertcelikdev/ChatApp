using System.ComponentModel.DataAnnotations;

namespace ChatApp.Models;

public class Country
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = "";
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation property
    public virtual ICollection<City> Cities { get; set; } = new List<City>();
}
