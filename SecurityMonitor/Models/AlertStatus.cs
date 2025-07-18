namespace SecurityMonitor.Models;

/// <summary>
/// Trạng thái của cảnh báo
/// </summary>
public class AlertStatus
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public string? Color { get; set; }
    public bool IsTerminal { get; set; }

    // Quan hệ
    public virtual ICollection<Alert> Alerts { get; set; } = new List<Alert>();
}
