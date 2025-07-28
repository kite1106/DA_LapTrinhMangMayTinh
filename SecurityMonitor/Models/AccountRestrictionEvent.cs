using System;

namespace SecurityMonitor.Models
{
    public class AccountRestrictionEvent
    {
        public int Id { get; set; }
        public string UserId { get; set; }
        public string UserName { get; set; }
        public string RestrictionReason { get; set; }
        public DateTimeOffset Timestamp { get; set; }
        public string Notes { get; set; }
        
        // Navigation property
        public virtual ApplicationUser User { get; set; }
    }
}
