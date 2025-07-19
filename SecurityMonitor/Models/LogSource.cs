namespace SecurityMonitor.Models;
using System.ComponentModel.DataAnnotations.Schema;

/// <summary>
/// Nguồn log trong hệ thống 
/// </summary>
public class LogSource
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string? IpAddress { get; set; }
    public string? DeviceType { get; set; }
    public string? Location { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? LastSeenAt { get; set; }

    // Quan hệ
    public virtual ICollection<Log> Logs { get; set; } = new List<Log>();
}
