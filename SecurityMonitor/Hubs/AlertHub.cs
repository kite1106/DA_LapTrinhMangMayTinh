using Microsoft.AspNetCore.SignalR;
using SecurityMonitor.DTOs;

namespace SecurityMonitor.Hubs;

/// <summary>
/// Hub SignalR để gửi cảnh báo và cập nhật trạng thái thời gian thực
/// </summary>
public class AlertHub : Hub
{
    public async Task SendLoginAlert(string email, string ip, int failedAttempts, string severity)
    {
        await Clients.All.SendAsync("ReceiveLoginAlert", new
        {
            title = "Cảnh báo đăng nhập thất bại",
            description = $"Phát hiện {failedAttempts} lần đăng nhập thất bại từ địa chỉ IP: {ip}",
            email = email,
            ip = ip,
            failedAttempts = failedAttempts,
            severity = severity,
            timestamp = DateTime.Now
        });
    }

    public async Task SendHighRiskLoginAlert(string email, string ip, string reason)
    {
        await Clients.All.SendAsync("ReceiveLoginAlert", new
        {
            title = "Cảnh báo đăng nhập rủi ro cao",
            description = $"Phát hiện đăng nhập rủi ro cao từ IP: {ip}. Lý do: {reason}",
            email = email,
            ip = ip,
            severity = "High",
            timestamp = DateTime.Now
        });
    }

    /// <summary>
    /// Gửi cập nhật thống kê dashboard real-time
    /// </summary>
    public async Task SendDashboardStatsUpdate(object stats)
    {
        await Clients.All.SendAsync("ReceiveDashboardStats", stats);
    }

    /// <summary>
    /// Gửi cập nhật số lượng cảnh báo real-time
    /// </summary>
    public async Task SendAlertCountsUpdate(object alertCounts)
    {
        await Clients.All.SendAsync("ReceiveAlertCounts", alertCounts);
    }

    /// <summary>
    /// Gửi cập nhật bảng cảnh báo real-time
    /// </summary>
    public async Task SendAlertsTableUpdate(object alertsData)
    {
        await Clients.All.SendAsync("ReceiveAlertsTableUpdate", alertsData);
    }

    /// <summary>
    /// Gửi cập nhật thống kê người dùng real-time
    /// </summary>
    public async Task SendUserStatsUpdate(object userStats)
    {
        await Clients.All.SendAsync("ReceiveUserStats", userStats);
    }

    /// <summary>
    /// Gửi cập nhật lịch sử đăng nhập real-time
    /// </summary>
    public async Task SendLoginHistoryUpdate(object loginHistory)
    {
        await Clients.All.SendAsync("ReceiveLoginHistory", loginHistory);
    }

    /// <summary>
    /// Gửi cập nhật cảnh báo gần đây real-time
    /// </summary>
    public async Task SendRecentAlertsUpdate(object recentAlerts)
    {
        await Clients.All.SendAsync("ReceiveRecentAlerts", recentAlerts);
    }

    /// <summary>
    /// Gửi cập nhật biểu đồ real-time
    /// </summary>
    public async Task SendChartDataUpdate(string chartType, object chartData)
    {
        await Clients.All.SendAsync("ReceiveChartData", chartType, chartData);
    }

    /// <summary>
    /// Gửi cập nhật metrics bảo mật real-time
    /// </summary>
    public async Task SendSecurityMetricsUpdate(object securityMetrics)
    {
        await Clients.All.SendAsync("ReceiveSecurityMetrics", securityMetrics);
    }

    public async Task JoinAlertGroup(string severity)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, severity);
    }

    public async Task LeaveAlertGroup(string severity)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, severity);
    }

    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();
        await Clients.Caller.SendAsync("ConnectionEstablished");
    }
}
