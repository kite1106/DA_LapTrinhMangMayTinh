namespace SecurityMonitor.DTOs.Alerts;

public class AlertUpdateDto
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public int? SeverityLevelId { get; set; }
    public int? StatusId { get; set; }
} 