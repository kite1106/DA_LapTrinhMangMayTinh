using SecurityMonitor.DTOs;

namespace SecurityMonitor.Services.Interfaces;

/// <summary>
/// Service để quản lý cập nhật real-time
/// </summary>
public interface IRealTimeUpdateService
{
    /// <summary>
    /// Gửi cập nhật thống kê dashboard
    /// </summary>
    Task SendDashboardStatsUpdateAsync();

    /// <summary>
    /// Gửi cập nhật số lượng cảnh báo
    /// </summary>
    Task SendAlertCountsUpdateAsync();

    /// <summary>
    /// Gửi cập nhật bảng cảnh báo
    /// </summary>
    Task SendAlertsTableUpdateAsync();

    /// <summary>
    /// Gửi cập nhật thống kê người dùng
    /// </summary>
    Task SendUserStatsUpdateAsync(string userId);

    /// <summary>
    /// Gửi cập nhật lịch sử đăng nhập
    /// </summary>
    Task SendLoginHistoryUpdateAsync(string userId);

    /// <summary>
    /// Gửi cập nhật cảnh báo gần đây
    /// </summary>
    Task SendRecentAlertsUpdateAsync(string userId);

    /// <summary>
    /// Gửi cập nhật biểu đồ
    /// </summary>
    Task SendChartDataUpdateAsync(string chartType, object chartData);

    /// <summary>
    /// Gửi cập nhật metrics bảo mật
    /// </summary>
    Task SendSecurityMetricsUpdateAsync();

    /// <summary>
    /// Bắt đầu background service để gửi cập nhật định kỳ
    /// </summary>
    Task StartPeriodicUpdatesAsync();

    /// <summary>
    /// Dừng background service
    /// </summary>
    Task StopPeriodicUpdatesAsync();
} 