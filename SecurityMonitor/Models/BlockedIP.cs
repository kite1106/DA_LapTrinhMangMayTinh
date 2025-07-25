using System;
using System.ComponentModel.DataAnnotations;

namespace SecurityMonitor.Models
{
    public class BlockedIP
    {
        public int Id { get; set; }
        
        [Required]
        [MaxLength(45)]  // IPv6 có thể dài tới 45 ký tự
        public required string IpAddress { get; set; }
        
        [Required]
        public required string Reason { get; set; }
        
        [Required]
        public required string BlockedBy { get; set; }
        
        public DateTime BlockedAt { get; set; } = DateTime.UtcNow;
    }
}
