using Microsoft.AspNetCore.SignalR;

namespace SecurityMonitor.Hubs;

/// <summary>
/// Hub SignalR để gửi cảnh báo thời gian thực
/// </summary>
public class AlertHub : Hub
{
    public async Task SendAlert(string message)
    {
        await Clients.All.SendAsync("ReceiveAlert", message);
    }

    public async Task JoinAlertGroup(string severity)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, severity);
    }

    public async Task LeaveAlertGroup(string severity)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, severity);
    }
}
