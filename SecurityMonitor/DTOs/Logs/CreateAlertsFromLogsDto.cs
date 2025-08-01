namespace SecurityMonitor.DTOs.Logs;

public class CreateAlertsFromLogsDto
{
    public string? Filter { get; set; }
    public int? MaxAlerts { get; set; }
    public bool IncludeErrors { get; set; } = true;
    public bool IncludeSecurity { get; set; } = true;
    public bool IncludeFailed { get; set; } = true;
} 