using System;
using System.ComponentModel.DataAnnotations;

namespace SecurityMonitor.DTOs.Security
{
    /// <summary>
    /// DTO cho việc quản lý hạn chế tài khoản
    /// </summary>
    public class AccountRestrictionDto
    {
        [Required]
        public required string UserId { get; set; }

        [Required]
        public required string RestrictionType { get; set; } // "Temporary", "Disable", "ReadOnly"

        [Required]
        public required string Reason { get; set; }

        public DateTimeOffset? EndTime { get; set; }

        public string? Notes { get; set; }
    }

    /// <summary>
    /// DTO cho hiển thị thông tin hạn chế tài khoản
    /// </summary>
    public class AccountRestrictionInfoDto
    {
        public int Id { get; set; }
        public required string UserId { get; set; }
        public required string UserName { get; set; }
        public string? FullName { get; set; }
        public required string RestrictionType { get; set; }
        public required string Reason { get; set; }
        public required string RestrictedBy { get; set; }
        public required string RestrictedByName { get; set; }
        public DateTimeOffset StartTime { get; set; }
        public DateTimeOffset? EndTime { get; set; }
        public bool IsActive { get; set; }
        public required string Notes { get; set; }
    }
}
