using System.ComponentModel.DataAnnotations;
using SecurityMonitor.Models;
using SecurityMonitor.Helpers;

namespace SecurityMonitor.DTOs.Alerts;

/// <summary>
/// DTO cho danh sách cảnh báo
/// </summary>
public record AlertListDto
{
    public int Id { get; init; }
    public DateTime Timestamp { get; init; }
    public required string Title { get; init; }
    public string? Description { get; init; }
    public required string SourceIp { get; init; }
    public required string SeverityLevel { get; init; }
    public required string Status { get; init; }
    public string? AssignedTo { get; init; }

    public string SeverityClass => AlertCssHelper.GetSeverityClass(SeverityLevel);
    public string StatusClass => AlertCssHelper.GetStatusClass(Status);
};

/// <summary>
/// DTO cho chi tiết cảnh báo
/// </summary>
public record AlertDetailDto
{
    public int Id { get; init; }
    public DateTime Timestamp { get; init; }
    public required string Title { get; init; }
    public string? Description { get; init; }
    public required string AlertType { get; init; }
    public required string SeverityLevel { get; init; }
    public required string Status { get; init; }
    public string? SourceIp { get; init; }
    public string? TargetIp { get; init; }
    public string? AssignedTo { get; init; }
    public string? ResolvedBy { get; init; }
    public DateTime? ResolvedAt { get; init; }
    public string? Resolution { get; init; }

    public string SeverityClass => AlertCssHelper.GetSeverityClass(SeverityLevel);
    public string StatusClass => AlertCssHelper.GetStatusClass(Status);
};

/// <summary>
/// DTO cho tạo cảnh báo mới
/// </summary>
public record CreateAlertDto(
    [Required(ErrorMessage = "Tiêu đề là bắt buộc")]
    string Title,

    string? Description,

    [Required(ErrorMessage = "Loại cảnh báo là bắt buộc")]
    AlertTypeId AlertTypeId,

    [Required(ErrorMessage = "Mức độ nghiêm trọng là bắt buộc")]
    SeverityLevelId SeverityLevelId,

    [Required(ErrorMessage = "IP nguồn là bắt buộc")]
    string SourceIp,

    string? TargetIp
);

/// <summary>
/// DTO cho cập nhật trạng thái cảnh báo
/// </summary>
public record UpdateAlertStatusDto(
    [Required(ErrorMessage = "Trạng thái là bắt buộc")]
    AlertStatusId Status,

    string? AssignTo
);

/// <summary>
/// DTO cho xử lý cảnh báo
/// </summary>
public record ResolveAlertDto(
    [Required(ErrorMessage = "Cách giải quyết là bắt buộc")]
    [MinLength(10, ErrorMessage = "Cách giải quyết phải có ít nhất 10 ký tự")]
    string Resolution
);
