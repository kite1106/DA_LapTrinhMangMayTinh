public record AlertRawDetailsDto
{
    public int Id { get; init; }
    public DateTime Timestamp { get; init; }
    public string Title { get; init; } = null!;
    public string? Description { get; init; }
    public string AlertType { get; init; } = null!;
    public string SeverityLevel { get; init; } = null!;
    public string Status { get; init; } = null!;
    public string? SourceIp { get; init; }
    public string? TargetIp { get; init; }
    public int[]? Ports { get; init; }
    public string? Protocol { get; init; }
    public string? UserAgent { get; init; }
    public string? RequestPath { get; init; }
    public string? RawData { get; init; }
}
