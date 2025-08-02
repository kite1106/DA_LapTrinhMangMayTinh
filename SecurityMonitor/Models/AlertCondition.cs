using System.ComponentModel.DataAnnotations;

namespace SecurityMonitor.Models
{
    public class AlertCondition
    {
        [Key]
        public int Id { get; set; }
        
        public int AlertRuleId { get; set; }
        
        [Required]
        [StringLength(50)]
        public string Field { get; set; } = string.Empty;
        
        [Required]
        [StringLength(20)]
        public string Operator { get; set; } = string.Empty; // equals, contains, greater_than, etc.
        
        [Required]
        public string Value { get; set; } = string.Empty;
        
        public bool IsActive { get; set; } = true;
        
        public int Order { get; set; } = 0;
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        // Navigation properties
        public virtual AlertRule AlertRule { get; set; } = null!;
    }
} 