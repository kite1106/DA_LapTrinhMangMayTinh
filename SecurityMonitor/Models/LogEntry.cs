using System.ComponentModel.DataAnnotations;

namespace SecurityMonitor.Models
{
    public class LogEntry
    {
        [Key]
        public long Id { get; set; }
        
        [Required]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        
        public int LogSourceId { get; set; }
        public int LogLevelTypeId { get; set; }
        public int? LogComponentId { get; set; }
        public int? LogEventTypeId { get; set; }
        public int? LogSeverityId { get; set; }
        
        [Required]
        [StringLength(500)]
        public string Message { get; set; } = string.Empty;
        
        [StringLength(1000)]
        public string? Details { get; set; }
        
        [StringLength(45)]
        public string? IpAddress { get; set; }
        
        [StringLength(100)]
        public string? UserId { get; set; }
        
        [StringLength(50)]
        public string? SessionId { get; set; }
        
        public bool WasSuccessful { get; set; } = true;
        
        public DateTime? ProcessedAt { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        // Navigation properties
        public virtual LogSource LogSource { get; set; } = null!;
        public virtual LogLevelType LogLevelType { get; set; } = null!;
        public virtual LogComponent? LogComponent { get; set; }
        public virtual LogEventType? LogEventType { get; set; }
        public virtual LogSeverity? LogSeverity { get; set; }
        public virtual ICollection<LogEntryTag> LogEntryTags { get; set; } = new List<LogEntryTag>();
        public virtual ICollection<LogAnalysis> LogAnalyses { get; set; } = new List<LogAnalysis>();
        public virtual ICollection<Alert> Alerts { get; set; } = new List<Alert>();
    }
} 