namespace SecurityMonitor.DTOs;

public record AlertDto(
    int Id,
    DateTime Timestamp,
    string Title,
    string? Description,
    string AlertType,
    string SeverityLevel,
    string Status,
    string? SourceIp,
    string? TargetIp,
    string? AssignedTo,
    string? ResolvedBy,
    DateTime? ResolvedAt,
    string? Resolution
);

public record CreateAlertDto(
    string Title,
    string? Description,
    int AlertTypeId,
    int SeverityLevelId,
    string? SourceIp,
    string? TargetIp,
    long? LogId
);

public record UpdateAlertDto(
    string Title,
    string? Description,
    int AlertTypeId,
    int SeverityLevelId,
    int StatusId
);

public record ResolveAlertDto(
    string Resolution
);

public record LogDto(
    long Id,
    DateTime Timestamp,
    string LogSource,
    string? EventType,
    string? Message,
    string? IpAddress,
    DateTime? ProcessedAt
);

public record CreateLogDto(
    int LogSourceId,
    string? EventType,
    string? Message,
    string? RawData,
    string? IpAddress
);

public record AuditLogDto(
    long Id,
    DateTime Timestamp,
    string? UserName,
    string Action,
    string EntityType,
    string EntityId,
    string? Details,
    string? IpAddress
);
 public class ConfidenceScoreResult
    {
        public string IpAddress { get; set; }
        public int ConfidenceScore { get; set; }
    }

    public class ReportStatistics
    {
        public int TotalReports { get; set; }
        public int NumDistinctIPs { get; set; }
        public int ReportedToday { get; set; }
        public bool IsWhitelisted { get; set; }
    }
public class LoginDto
{
    public string Username { get; set; } = null!;
    public string Password { get; set; } = null!;
}