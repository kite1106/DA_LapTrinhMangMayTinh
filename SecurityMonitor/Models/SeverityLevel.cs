namespace SecurityMonitor.Models;

/// <summary>
/// Mức độ nghiêm trọng của cảnh báo
/// </summary>
public class SeverityLevel
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public string? Color { get; set; }
    public int Priority { get; set; }

    // Quan hệ
    public virtual ICollection<Alert> Alerts { get; set; } = new List<Alert>();
}
