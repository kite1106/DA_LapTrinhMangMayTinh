using System.ComponentModel.DataAnnotations;

namespace SecurityMonitor.Models
{
    public class LogTag
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        [StringLength(50)]
        public string Name { get; set; } = string.Empty;
        
        [StringLength(100)]
        public string? Description { get; set; }
        
        [StringLength(7)]
        public string? Color { get; set; }
        
        public bool IsActive { get; set; } = true;
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        // Navigation properties
        public virtual ICollection<LogEntryTag> LogEntryTags { get; set; } = new List<LogEntryTag>();
    }
} 