using System;
using System.Collections.Generic;

namespace SecurityMonitor.DTOs
{
    public class UserManagementDto
    {
        public string Id { get; set; }
        public string Email { get; set; }
        public string UserName { get; set; }
        public bool IsLocked { get; set; }
        public DateTimeOffset? LockoutEnd { get; set; }
        public DateTime? LastLoginTime { get; set; }
        public int FailedAccessCount { get; set; }
        public IList<string> Roles { get; set; }
    }
}
