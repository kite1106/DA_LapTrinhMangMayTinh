using System;

namespace SecurityMonitor.Models
{
    public class AccountRestriction
    {
        public int Id { get; set; }
        public string UserId { get; set; }
        public string RestrictedBy { get; set; }
        public string RestrictionType { get; set; } // "Temporary", "Disable", "ReadOnly"
        public string Reason { get; set; }
        public DateTimeOffset StartTime { get; set; }
        public DateTimeOffset? EndTime { get; set; }
        public bool IsActive { get; set; }
        public string Notes { get; set; }
        
        // Navigation properties
        public virtual ApplicationUser User { get; set; }
        public virtual ApplicationUser RestrictedByUser { get; set; }
    }
}
