namespace SecurityMonitor.Models;

/// <summary>
/// Nhật ký hoạt động người dùng
/// </summary>
public class AuditLog
{
    public long Id { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string? UserId { get; set; }
    public string Action { get; set; } = null!;
    public string EntityType { get; set; } = null!;
    public string EntityId { get; set; } = null!;
    public string? Details { get; set; }
    public string? IpAddress { get; set; }

    // Quan hệ
    public virtual ApplicationUser? User { get; set; }
}
