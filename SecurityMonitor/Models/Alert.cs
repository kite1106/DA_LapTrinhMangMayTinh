namespace SecurityMonitor.Models;

/// <summary>
/// Cảnh báo an ninh
/// </summary>
public class Alert
{
    public int Id { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public int AlertTypeId { get; set; }
    public int SeverityLevelId { get; set; }
    public int StatusId { get; set; }
    public string? SourceIp { get; set; }
    public string? TargetIp { get; set; }
    public long? LogId { get; set; }
    public string? AssignedToId { get; set; }
    public string? ResolvedById { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? Resolution { get; set; }

    // Quan hệ
    public virtual AlertType AlertType { get; set; } = null!;
    public virtual SeverityLevel SeverityLevel { get; set; } = null!;
    public virtual AlertStatus Status { get; set; } = null!;
    public virtual LogEntry? Log { get; set; }
    public virtual ApplicationUser? AssignedTo { get; set; }
    public virtual ApplicationUser? ResolvedBy { get; set; }
}
