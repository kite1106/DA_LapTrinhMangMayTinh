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
    public DateTime? LastLoginAt { get; set; }
    public bool IsActive { get; set; } = true;

    // Các quan hệ
    public virtual ICollection<Alert> AssignedAlerts { get; set; } = new List<Alert>();
    public virtual ICollection<Alert> ResolvedAlerts { get; set; } = new List<Alert>();
    public virtual ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
}
