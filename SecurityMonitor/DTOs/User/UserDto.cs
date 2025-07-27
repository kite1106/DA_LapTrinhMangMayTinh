using System;

namespace SecurityMonitor.DTOs
{
    public class UserDto
    {
        public string Id { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string? FullName { get; set; }
        public bool IsEmailConfirmed { get; set; }
        public DateTime? LastLoginTime { get; set; }
        public int LoginCount { get; set; }
    }
}
