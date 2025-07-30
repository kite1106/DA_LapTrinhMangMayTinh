using System.ComponentModel.DataAnnotations;

namespace SecurityMonitor.Models
{
    public class LogSeverity
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        [StringLength(20)]
        public string Name { get; set; } = string.Empty;
        
        [StringLength(100)]
        public string? Description { get; set; }
        
        public int Priority { get; set; } = 0;
        
        [StringLength(7)]
        public string? Color { get; set; }
        
        public bool IsActive { get; set; } = true;
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        // Navigation properties
        public virtual ICollection<LogEntry> LogEntries { get; set; } = new List<LogEntry>();
    }
} 