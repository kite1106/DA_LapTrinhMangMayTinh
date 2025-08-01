using System.ComponentModel.DataAnnotations;

namespace SecurityMonitor.Models
{
    public class LogEntryTag
    {
        [Key]
        public int Id { get; set; }
        
        public long LogEntryId { get; set; }
        public int LogTagId { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        // Navigation properties
        public virtual LogEntry LogEntry { get; set; } = null!;
        public virtual LogTag LogTag { get; set; } = null!;
    }
} 