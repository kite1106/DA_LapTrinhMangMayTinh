using SecurityMonitor.DTOs.Common;
using SecurityMonitor.DTOs.Auth;

namespace SecurityMonitor.DTOs.Dashboard;

/// <summary>
/// DTO cho thống kê tổng quan của admin
/// </summary>
public record AdminDashboardDto(
    int TotalUsers,
    int TotalAlerts,
    int CriticalAlerts,
    int HighAlerts,
    int AlertsInProgress,
    int ResolvedAlerts,
    int TotalLogins,
    int UniqueSources,
    List<AlertSummaryDto> RecentAlerts
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
