using System.ComponentModel.DataAnnotations;

namespace SecurityMonitor.Models
{
    public class LogAnalysis
    {
        [Key]
        public int Id { get; set; }
        
        public long LogEntryId { get; set; }
        
        [StringLength(50)]
        public string AnalysisType { get; set; } = string.Empty;
        
        [StringLength(200)]
        public string? AnalysisResult { get; set; }
        
        public decimal? ConfidenceScore { get; set; }
        
        [StringLength(20)]
        public string? RiskLevel { get; set; }
        
        public bool IsAnomaly { get; set; } = false;
        
        public bool IsThreat { get; set; } = false;
        
        [StringLength(500)]
        public string? Recommendations { get; set; }
        
        public DateTime AnalyzedAt { get; set; } = DateTime.UtcNow;
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        // Navigation properties
        public virtual LogEntry LogEntry { get; set; } = null!;
    }
} 