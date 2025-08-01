using Microsoft.AspNetCore.SignalR;
using SecurityMonitor.Services.Interfaces;
using SecurityMonitor.Models;
using SecurityMonitor.DTOs.Security;

namespace SecurityMonitor.Hubs;

public class AlertHub : Hub
{
    private readonly IAlertService _alertService;
    private readonly ILogger<AlertHub> _logger;

    public AlertHub(IAlertService alertService, ILogger<AlertHub> logger)
    {
        _alertService = alertService;
        _logger = logger;
    }

    public async Task JoinAlertGroup()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "Alerts");
        _logger.LogInformation("Client {ConnectionId} joined Alerts group", Context.ConnectionId);
    }

    public async Task LeaveAlertGroup()
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "Alerts");
        _logger.LogInformation("Client {ConnectionId} left Alerts group", Context.ConnectionId);
    }

    public async Task NotifyNewAlert(Alert alert)
    {
        var alertDto = new
        {
            id = alert.Id,
            title = alert.Title,
            description = alert.Description,
            alertType = alert.AlertType?.Name,
            severityLevel = alert.SeverityLevel?.Name,
            status = alert.Status?.Name,
            sourceIp = alert.SourceIp,
            timestamp = alert.Timestamp
        };

        await Clients.Group("Alerts").SendAsync("ReceiveNewAlert", alertDto);
        _logger.LogInformation("Notified clients about new alert {AlertId}", alert.Id);
    }

    public async Task NotifyAlertUpdate(int alertId, string newStatus)
    {
        var alert = await _alertService.GetAlertByIdAsync(alertId);
        if (alert != null)
        {
            var alertDto = new
            {
                id = alert.Id,
                title = alert.Title,
                description = alert.Description,
                alertType = alert.AlertType?.Name,
                severityLevel = alert.SeverityLevel?.Name,
                status = alert.Status?.Name,
                sourceIp = alert.SourceIp,
                timestamp = alert.Timestamp
            };

            await Clients.Group("Alerts").SendAsync("ReceiveAlertUpdate", alertDto);
            _logger.LogInformation("Notified clients about alert update {AlertId}", alertId);
        }
    }

    public async Task NotifyAlertDelete(int alertId)
    {
        await Clients.Group("Alerts").SendAsync("ReceiveAlertDelete", alertId);
        _logger.LogInformation("Notified clients about alert deletion {AlertId}", alertId);
    }

    public async Task UpdateDashboardStats(SecurityMetricsDto stats)
    {
        await Clients.Group("Alerts").SendAsync("ReceiveDashboardStats", stats);
        _logger.LogInformation("Updated dashboard stats for all clients");
    }

    // Security action methods
    public async Task ForceLogout(string userId, string message)
    {
        await Clients.User(userId).SendAsync("ForceLogout", message);
        _logger.LogInformation("Forced logout for user {UserId}: {Message}", userId, message);
    }

    public async Task RedirectToUserDashboard(string userId, string message)
    {
        await Clients.User(userId).SendAsync("RedirectToUserDashboard", message);
        _logger.LogInformation("Redirected user {UserId} to dashboard: {Message}", userId, message);
    }

    public async Task BlockIP(string ipAddress, string reason)
    {
        await Clients.All.SendAsync("BlockIP", ipAddress, reason);
        _logger.LogInformation("Blocked IP {IPAddress}: {Reason}", ipAddress, reason);
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}

// DTOs for real-time updates
public class ActivityUpdateDto
{
    public DateTime Timestamp { get; set; }
    public string UserName { get; set; } = "";
    public string Action { get; set; } = "";
    public string IpAddress { get; set; } = "";
    public bool Success { get; set; }
}

public class DashboardStatsDto
{
    public int TotalAlerts { get; set; }
    public int ActiveUsers { get; set; }
    public int BlockedIPs { get; set; }
    public int RestrictedUsers { get; set; }
    public int RecentAlerts { get; set; }
    public bool IsLogGenerationActive { get; set; }
}
