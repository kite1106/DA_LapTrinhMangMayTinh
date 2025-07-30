using System.ComponentModel.DataAnnotations;

namespace SecurityMonitor.Models
{
    public class AlertRule
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;
        
        [StringLength(500)]
        public string? Description { get; set; }
        
        [Required]
        [StringLength(50)]
        public string RuleType { get; set; } = string.Empty; // Pattern, Threshold, Anomaly
        
        [Required]
        public string Condition { get; set; } = string.Empty; // JSON condition
        
        public bool IsActive { get; set; } = true;
        
        public int Priority { get; set; } = 0;
        
        public int SeverityId { get; set; }
        
        [StringLength(100)]
        public string? AlertMessage { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public DateTime? UpdatedAt { get; set; }
        
        // Navigation properties
        public virtual LogSeverity Severity { get; set; } = null!;
        public virtual ICollection<AlertCondition> AlertConditions { get; set; } = new List<AlertCondition>();
    }
} 