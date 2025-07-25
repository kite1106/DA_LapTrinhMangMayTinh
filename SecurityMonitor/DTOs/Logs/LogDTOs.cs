namespace SecurityMonitor.DTOs.Logs;

/// <summary>
/// DTO cho nhật ký hệ thống
/// </summary>
public record LogDto(
    long Id,
    DateTime Timestamp,
    string? IpAddress,
    string Message,
    string Level = "Information",
    string SourceName = "",
    string? Details = null,
    bool IsProcessed = false,
    DateTime? ProcessedAt = null
);

/// <summary>
/// DTO cho nhật ký hoạt động của user
/// </summary>
public record MyLogDto(
    DateTime Timestamp,
    string? IpAddress,
    string LogSourceName,
    string EventType,
    bool WasSuccessful,
    string? Message = null
);

/// <summary>
/// DTO cho nhật ký kiểm toán
/// </summary>
public record AuditLogDto(
    long Id,
    DateTime Timestamp,
    string? UserEmail,
    string Action,
    string EntityType,
    string EntityId,
    string? Details = null,
    string? IpAddress = null
);
