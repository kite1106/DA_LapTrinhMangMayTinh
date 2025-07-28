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
