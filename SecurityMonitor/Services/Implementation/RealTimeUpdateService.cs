using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using SecurityMonitor.Hubs;
using SecurityMonitor.Services.Interfaces;
using SecurityMonitor.Data;
using SecurityMonitor.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SecurityMonitor.Services.Implementation;

/// <summary>
/// Service để quản lý cập nhật real-time
/// </summary>
public class RealTimeUpdateService : IRealTimeUpdateService, IDisposable
{
    private readonly IHubContext<AlertHub> _alertHubContext;
    private readonly ILogger<RealTimeUpdateService> _logger;
    private readonly ApplicationDbContext _context;
    private readonly IAlertService _alertService;
    private Timer? _dashboardStatsTimer;
    private Timer? _alertCountsTimer;
    private Timer? _userStatsTimer;
    private Timer? _securityMetricsTimer;
    private bool _disposed = false;

    public RealTimeUpdateService(
        IHubContext<AlertHub> alertHubContext,
        ILogger<RealTimeUpdateService> logger,
        ApplicationDbContext context,
        IAlertService alertService)
    {
        _alertHubContext = alertHubContext;
        _logger = logger;
        _context = context;
        _alertService = alertService;
    }

    public async Task SendDashboardStatsUpdateAsync()
    {
        try
        {
            // Lấy thống kê dashboard
            var stats = new
            {
                totalUsers = await _context.Users.CountAsync(),
                totalAlerts = await _context.Alerts.CountAsync(),
                blockedIPs = await _context.BlockedIPs.CountAsync(),
                totalLogs = await _context.Logs.CountAsync(),
                timestamp = DateTime.Now
            };

            await _alertHubContext.Clients.All.SendAsync("ReceiveDashboardStats", stats);
            _logger.LogInformation("Dashboard stats update sent");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending dashboard stats update");
        }
    }

    public async Task SendAlertCountsUpdateAsync()
    {
        try
        {
            var alertCounts = new
            {
                critical = await _context.Alerts.CountAsync(a => a.SeverityLevelId == (int)SeverityLevelId.Critical),
                high = await _context.Alerts.CountAsync(a => a.SeverityLevelId == (int)SeverityLevelId.High),
                medium = await _context.Alerts.CountAsync(a => a.SeverityLevelId == (int)SeverityLevelId.Medium),
                low = await _context.Alerts.CountAsync(a => a.SeverityLevelId == (int)SeverityLevelId.Low),
                inProgress = await _context.Alerts.CountAsync(a => a.StatusId == (int)AlertStatusId.InProgress),
                resolved = await _context.Alerts.CountAsync(a => a.StatusId == (int)AlertStatusId.Resolved),
                timestamp = DateTime.Now
            };

            await _alertHubContext.Clients.All.SendAsync("ReceiveAlertCounts", alertCounts);
            _logger.LogInformation("Alert counts update sent");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending alert counts update");
        }
    }

    public async Task SendAlertsTableUpdateAsync()
    {
        try
        {
            var recentAlerts = await _context.Alerts
                .OrderByDescending(a => a.Timestamp)
                .Take(50)
                .Select(a => new
                {
                    a.Id,
                    a.Title,
                    a.Description,
                    a.SeverityLevelId,
                    a.StatusId,
                    a.Timestamp,
                    a.SourceIp
                })
                .ToListAsync();

            await _alertHubContext.Clients.All.SendAsync("ReceiveAlertsTableUpdate", recentAlerts);
            _logger.LogInformation("Alerts table update sent");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending alerts table update");
        }
    }

    public async Task SendUserStatsUpdateAsync(string userId)
    {
        try
        {
            var userStats = new
            {
                recentLogins = await _context.AuditLogs
                    .Where(a => a.UserId == userId)
                    .CountAsync(a => a.Timestamp >= DateTime.Now.AddDays(7)),
                totalAlerts = await _context.Alerts
                    .CountAsync(a => a.SourceIp == userId),
                importantAlerts = await _context.Alerts
                    .CountAsync(a => a.SourceIp == userId && 
                                   (a.SeverityLevelId == (int)SeverityLevelId.High || 
                                    a.SeverityLevelId == (int)SeverityLevelId.Critical)),
                timestamp = DateTime.Now
            };

            await _alertHubContext.Clients.All.SendAsync("ReceiveUserStats", userStats);
            _logger.LogInformation("User stats update sent for user {UserId}", userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending user stats update for user {UserId}", userId);
        }
    }

    public async Task SendLoginHistoryUpdateAsync(string userId)
    {
        try
        {
            var loginHistory = await _context.AuditLogs
                .Where(a => a.UserId == userId)
                .OrderByDescending(a => a.Timestamp)
                .Take(10)
                .Select(a => new
                {
                    a.Timestamp,
                    a.IpAddress,
                    Success = a.StatusCode < 400
                })
                .ToListAsync();

            await _alertHubContext.Clients.All.SendAsync("ReceiveLoginHistory", loginHistory);
            _logger.LogInformation("Login history update sent for user {UserId}", userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending login history update for user {UserId}", userId);
        }
    }

    public async Task SendRecentAlertsUpdateAsync(string userId)
    {
        try
        {
            var recentAlerts = await _context.Alerts
                .Where(a => a.SourceIp == userId)
                .OrderByDescending(a => a.Timestamp)
                .Take(10)
                .Select(a => new
                {
                    a.Id,
                    a.Title,
                    a.Description,
                    a.SeverityLevelId,
                    a.Timestamp
                })
                .ToListAsync();

            await _alertHubContext.Clients.All.SendAsync("ReceiveRecentAlerts", recentAlerts);
            _logger.LogInformation("Recent alerts update sent for user {UserId}", userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending recent alerts update for user {UserId}", userId);
        }
    }

    public async Task SendChartDataUpdateAsync(string chartType, object chartData)
    {
        try
        {
            await _alertHubContext.Clients.All.SendAsync("ReceiveChartData", chartType, chartData);
            _logger.LogInformation("Chart data update sent for chart type {ChartType}", chartType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending chart data update for chart type {ChartType}", chartType);
        }
    }

    public async Task SendSecurityMetricsUpdateAsync()
    {
        try
        {
            var securityMetrics = new
            {
                                 sensitiveEndpoints = new
                 {
                     totalAccessAttempts = await _context.LogEntries
                         .CountAsync(l => l.Message.Contains("/admin") || l.Message.Contains("/api")),
                     unauthorizedAttempts = await _context.LogEntries
                         .CountAsync(l => l.WasSuccessful == false && (l.Message.Contains("/admin") || l.Message.Contains("/api"))),
                     blockedAttempts = await _context.LogEntries
                         .CountAsync(l => l.WasSuccessful == false && l.Message.Contains("403"))
                 },
                 anomalies = new
                 {
                     highRequestRateIPs = await _context.LogEntries
                         .GroupBy(l => l.IpAddress)
                         .Where(g => g.Count() > 100)
                         .CountAsync(),
                     scanningAttempts = await _context.LogEntries
                         .CountAsync(l => l.Message.Contains("scan") || l.Message.Contains("probe")),
                    potentialDDoSAlerts = await _context.Alerts
                        .CountAsync(a => a.AlertTypeId == 1) // Assuming DDoS is type 1
                },
                timestamp = DateTime.Now
            };

            await _alertHubContext.Clients.All.SendAsync("ReceiveSecurityMetrics", securityMetrics);
            _logger.LogInformation("Security metrics update sent");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending security metrics update");
        }
    }

    public async Task StartPeriodicUpdatesAsync()
    {
        try
        {
            // Dashboard stats - cập nhật mỗi 30 giây
            _dashboardStatsTimer = new Timer(async _ => await SendDashboardStatsUpdateAsync(), null, TimeSpan.Zero, TimeSpan.FromSeconds(30));

            // Alert counts - cập nhật mỗi 10 giây
            _alertCountsTimer = new Timer(async _ => await SendAlertCountsUpdateAsync(), null, TimeSpan.Zero, TimeSpan.FromSeconds(10));

            // Security metrics - cập nhật mỗi 5 phút
            _securityMetricsTimer = new Timer(async _ => await SendSecurityMetricsUpdateAsync(), null, TimeSpan.Zero, TimeSpan.FromMinutes(5));

            _logger.LogInformation("Periodic updates started");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting periodic updates");
        }
    }

    public async Task StopPeriodicUpdatesAsync()
    {
        try
        {
            _dashboardStatsTimer?.Dispose();
            _alertCountsTimer?.Dispose();
            _userStatsTimer?.Dispose();
            _securityMetricsTimer?.Dispose();

            _dashboardStatsTimer = null;
            _alertCountsTimer = null;
            _userStatsTimer = null;
            _securityMetricsTimer = null;

            _logger.LogInformation("Periodic updates stopped");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping periodic updates");
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            _dashboardStatsTimer?.Dispose();
            _alertCountsTimer?.Dispose();
            _userStatsTimer?.Dispose();
            _securityMetricsTimer?.Dispose();
            _disposed = true;
        }
    }
} 