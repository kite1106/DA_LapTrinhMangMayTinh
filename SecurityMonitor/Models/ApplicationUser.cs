using Microsoft.AspNetCore.Identity;

namespace SecurityMonitor.Models;

/// <summary>
/// Model người dùng mở rộng từ IdentityUser
/// </summary>
public class ApplicationUser : IdentityUser
{
    public string? FullName { get; set; }
    public string? Department { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? LastLoginTime { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset? LockoutStart { get; set; }
    public string? LockoutReason { get; set; }
    public string? LastLoginIP { get; set; }
    public bool RequirePasswordChange { get; set; } = false;

    public bool IsRestricted { get; set; } = false;

    // Các quan hệ
    public virtual ICollection<Alert> AssignedAlerts { get; set; } = new List<Alert>();
    public virtual ICollection<Alert> ResolvedAlerts { get; set; } = new List<Alert>();
    public virtual ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
}
