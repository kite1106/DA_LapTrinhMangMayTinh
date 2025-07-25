using System.ComponentModel.DataAnnotations;

namespace SecurityMonitor.DTOs.Common;

/// <summary>
/// DTO cho hiển thị cảnh báo ngắn gọn
/// </summary>
public record AlertSummaryDto(
    DateTime Timestamp,
    string? SourceIp,
    string? Description,
    string SeverityLevel,
    string Status
);

/// <summary>
/// DTO cho lịch sử đăng nhập của người dùng
/// </summary>
public record UserLoginHistoryDto(
    DateTime Timestamp,
    string IpAddress,
    string Details,
    bool Success
);
