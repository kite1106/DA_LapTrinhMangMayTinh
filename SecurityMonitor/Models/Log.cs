namespace SecurityMonitor.Models;

/// <summary>
/// Log từ các nguồn trong hệ thống
/// </summary>
public class Log
{
    public long Id { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public int LogSourceId { get; set; }
    public string? EventType { get; set; }
    public string? Message { get; set; }
    public string? RawData { get; set; }
    public string? IpAddress { get; set; }
    public DateTime? ProcessedAt { get; set; }

    // Quan hệ
    public virtual LogSource LogSource { get; set; } = null!;
    public virtual ICollection<Alert> Alerts { get; set; } = new List<Alert>();
}
