namespace SecurityMonitor.DTOs.Logs;

public record CreateLogDto(
    int LogSourceId,
    string? EventType,
    string? Message,
    string? RawData,
    string? IpAddress
);
