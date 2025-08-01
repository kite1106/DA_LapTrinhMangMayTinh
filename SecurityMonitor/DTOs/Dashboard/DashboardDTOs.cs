using SecurityMonitor.DTOs.Common;
using SecurityMonitor.DTOs.Auth;

namespace SecurityMonitor.DTOs.Dashboard;

/// <summary>
/// DTO cho thống kê tổng quan của admin
/// </summary>
public record AdminDashboardDto(
    int TotalAlerts,
    int ActiveUsers,
    int BlockedIPs,
    int RestrictedUsers,
    int RecentAlerts,
    bool IsLogGenerationActive,
    List<AlertDto> RecentAlertsList,
    List<ActivityDto> RecentActivity
);

/// <summary>
/// DTO cho dashboard của user
/// </summary>
public record UserDashboardDto(
    int TotalAlerts,
    int ImportantAlerts,
    int RecentLogins,
    List<SecurityMonitor.DTOs.Logs.AuditLogDto> RecentLoginHistory,
    List<AlertSummaryDto> RecentAlerts
);

public class AlertDto
{
    public long Id { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string SourceIp { get; set; } = "";
    public DateTime Timestamp { get; set; }
    public string SeverityLevel { get; set; } = "";
    public string Type { get; set; } = "";
}

public class ActivityDto
{
    public DateTime Timestamp { get; set; }
    public string UserName { get; set; } = "";
    public string Action { get; set; } = "";
    public string IpAddress { get; set; } = "";
    public bool Success { get; set; }
}
