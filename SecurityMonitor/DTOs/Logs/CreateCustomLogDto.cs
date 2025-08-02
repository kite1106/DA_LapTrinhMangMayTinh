namespace SecurityMonitor.DTOs.Logs;

public class CreateCustomLogDto
{
    public int LogSourceId { get; set; }
    public int LogLevelTypeId { get; set; }
    public string IpAddress { get; set; } = "";
    public string UserId { get; set; } = "";
    public string Message { get; set; } = "";
    public bool CreateAlert { get; set; }
} 