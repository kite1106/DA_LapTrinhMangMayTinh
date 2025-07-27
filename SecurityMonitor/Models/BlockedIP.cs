using System;
using System.ComponentModel.DataAnnotations;

namespace SecurityMonitor.Models
{
    public class BlockedIP
    {
        // Constructor for creating new blocks
        public BlockedIP(string ipAddress, string reason, string blockedBy)
        {
            IpAddress = ipAddress;
            Reason = reason;
            BlockedBy = blockedBy;
            BlockedAt = DateTime.UtcNow;
        }

        // Parameterless constructor for EF Core
#pragma warning disable CS8618 // Non-nullable field is uninitialized
        public BlockedIP()
        {
            // This constructor is used by EF Core
        }
#pragma warning restore CS8618

        public int Id { get; set; }
        
        [Required]
        [MaxLength(45)]  // IPv6 có thể dài tới 45 ký tự
        public string IpAddress { get; set; }
        
        [Required]
        public string Reason { get; set; }
        
        [Required]
        public string BlockedBy { get; set; }
        
        public DateTime BlockedAt { get; set; }
    }
}
