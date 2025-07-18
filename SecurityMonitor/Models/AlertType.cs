namespace SecurityMonitor.Models;

/// <summary>
/// Loại cảnh báo
/// </summary>
public class AlertType
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public string? Category { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    // Quan hệ
    public virtual ICollection<Alert> Alerts { get; set; } = new List<Alert>();
}
