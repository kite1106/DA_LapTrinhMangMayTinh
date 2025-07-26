using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SecurityMonitor.Hubs;
using SecurityMonitor.Models;
using SecurityMonitor.Services.Interfaces;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace SecurityMonitor.Services.Implementation;

public class LogEventService : ILogEventService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LogEventService> _logger;
    private readonly IHubContext<AlertHub> _hubContext;
    private readonly ConcurrentDictionary<string, UserActivityTracker> _userTrackers;
    private readonly ConcurrentDictionary<string, EndpointTracker> _endpointTrackers;

    // Cấu hình các ngưỡng
    private const int RESET_PASSWORD_THRESHOLD = 3; // Số lần reset password trong 5 phút
    private const int CHANGE_EMAIL_THRESHOLD = 3; // Số lần đổi email trong 5 phút
    private const int TRACKING_WINDOW_MINUTES = 5;

    // Từ khóa đáng ngờ trong logs
    private static readonly string[] SUSPICIOUS_KEYWORDS = new[]
    {
        "injection", "xss", "csrf", "overflow", "bypass", "exploit",
        "unauthorized", "forbidden", "invalid token", "malicious"
    };

    // Các endpoint nhạy cảm cần theo dõi
    private static readonly string[] SENSITIVE_ENDPOINTS = new[]
    {
        "/admin", "/config", "/settings", "/users/all",
        "/api/admin", "/api/system", "/api/config",
        "/backup", "/logs", "/security"
    };

    public LogEventService(
        IServiceScopeFactory scopeFactory,
        IHubContext<AlertHub> hubContext,
        ILogger<LogEventService> logger)
    {
        _scopeFactory = scopeFactory;
        _hubContext = hubContext;
        _logger = logger;
        _userTrackers = new ConcurrentDictionary<string, UserActivityTracker>();
        _endpointTrackers = new ConcurrentDictionary<string, EndpointTracker>();
    }

    public async Task RecordApiEventAsync(string endpoint, string method, string userId, string ipAddress, int statusCode)
    {
        using var scope = _scopeFactory.CreateScope();
        var logService = scope.ServiceProvider.GetRequiredService<ILogService>();
        var alertService = scope.ServiceProvider.GetRequiredService<IAlertService>();

        // Lấy hoặc tạo LogSource cho Web Server
        var logSource = await EnsureLogSourceExistsAsync(logService, "Web Server", "Web Application", ipAddress);
        
        // Ghi nhận event vào log
        await logService.CreateLogAsync(new Log
        {
            Timestamp = DateTime.UtcNow,
            EventType = "API",
            Message = $"{method} {endpoint}",
            RawData = $"Status: {statusCode}, User: {userId}",
            IpAddress = ipAddress,
            LogSourceId = logSource.Id
        });

        // Kiểm tra endpoint nhạy cảm
        if (IsSensitiveEndpoint(endpoint))
        {
            var tracker = _endpointTrackers.GetOrAdd(endpoint, _ => new EndpointTracker(endpoint));
            tracker.RecordAccess(userId, ipAddress, statusCode);

            if (statusCode == 403 || statusCode == 401)
            {
                await CreateAlertAsync(alertService, 
                    "Truy cập trái phép vào endpoint nhạy cảm",
                    $"User {userId} từ IP {ipAddress} cố gắng truy cập {endpoint}",
                    ipAddress,
                    AlertTypeId.SuspiciousIP,
                    SeverityLevelId.High);
            }
        }

        // Phát hiện nhiều request 404 từ cùng một IP (có thể là scan)
        if (statusCode == 404)
        {
            var tracker = _endpointTrackers.GetOrAdd(ipAddress, _ => new EndpointTracker(ipAddress));
            tracker.RecordNotFound();

            if (tracker.NotFoundCount >= 10) // Ngưỡng 10 lần 404 trong 5 phút
            {
                await CreateAlertAsync(alertService,
                    "Phát hiện hoạt động scan",
                    $"IP {ipAddress} gửi {tracker.NotFoundCount} request không hợp lệ",
                    ipAddress,
                    AlertTypeId.SuspiciousIP,
                    SeverityLevelId.Medium);
            }
        }
    }

    public async Task RecordAuthEventAsync(string action, string userId, string ipAddress, bool isSuccessful)
    {
        using var scope = _scopeFactory.CreateScope();
        var logService = scope.ServiceProvider.GetRequiredService<ILogService>();
        var alertService = scope.ServiceProvider.GetRequiredService<IAlertService>();

        // Ghi log mọi sự kiện xác thực
        var logSource = await EnsureLogSourceExistsAsync(logService, "Authentication Server", "Authentication Service", ipAddress);
        
        await logService.CreateLogAsync(new Log
        {
            Timestamp = DateTime.UtcNow,
            EventType = "Authentication",
            Message = $"{action} - {(isSuccessful ? "Success" : "Failed")}",
            RawData = $"User: {userId}",
            IpAddress = ipAddress,
            LogSourceId = logSource.Id
        });

        var tracker = _userTrackers.GetOrAdd(userId, _ => new UserActivityTracker(userId));

        // Theo dõi các hành vi đáng ngờ
        if (action.Contains("ResetPassword", StringComparison.OrdinalIgnoreCase))
        {
            tracker.RecordPasswordReset();
            if (tracker.PasswordResetCount >= RESET_PASSWORD_THRESHOLD)
            {
                await CreateAlertAsync(alertService,
                    "Phát hiện nhiều lần reset password",
                    $"User {userId} từ IP {ipAddress} đã reset password {tracker.PasswordResetCount} lần trong {TRACKING_WINDOW_MINUTES} phút",
                    ipAddress,
                    AlertTypeId.SuspiciousIP,
                    SeverityLevelId.High);
            }
        }
        else if (action.Contains("ChangeEmail", StringComparison.OrdinalIgnoreCase))
        {
            tracker.RecordEmailChange();
            if (tracker.EmailChangeCount >= CHANGE_EMAIL_THRESHOLD)
            {
                await CreateAlertAsync(alertService,
                    "Phát hiện nhiều lần thay đổi email",
                    $"User {userId} từ IP {ipAddress} đã thay đổi email {tracker.EmailChangeCount} lần trong {TRACKING_WINDOW_MINUTES} phút",
                    ipAddress,
                    AlertTypeId.SuspiciousIP,
                    SeverityLevelId.High);
            }
        }
    }

    public async Task RecordSystemEventAsync(string eventType, string message, string source, string? ipAddress = null)
    {
        using var scope = _scopeFactory.CreateScope();
        var logService = scope.ServiceProvider.GetRequiredService<ILogService>();
        var alertService = scope.ServiceProvider.GetRequiredService<IAlertService>();

        // Ghi log sự kiện hệ thống
        var logSource = await EnsureLogSourceExistsAsync(logService, source, "System Service", ipAddress ?? "system");
        
        await logService.CreateLogAsync(new Log
        {
            Timestamp = DateTime.UtcNow,
            EventType = eventType,
            Message = message,
            RawData = $"Source: {source}",
            IpAddress = ipAddress,
            LogSourceId = logSource.Id
        });

        // Phát hiện từ khóa đáng ngờ trong log
        if (ContainsSuspiciousKeywords(message))
        {
            await CreateAlertAsync(alertService,
                "Phát hiện từ khóa đáng ngờ trong log",
                $"Log từ {source} chứa nội dung đáng ngờ: {message}",
                ipAddress ?? "system",
                AlertTypeId.SuspiciousIP,
                SeverityLevelId.Medium);
        }

        // Phân tích lỗi hệ thống
        if (eventType.Equals("Error", StringComparison.OrdinalIgnoreCase))
        {
            var sourceTracker = _endpointTrackers.GetOrAdd(source, _ => new EndpointTracker(source));
            sourceTracker.RecordError();

            if (sourceTracker.ErrorCount >= 5) // Ngưỡng 5 lỗi trong 5 phút
            {
                await CreateAlertAsync(alertService,
                    "Phát hiện nhiều lỗi hệ thống",
                    $"Nguồn {source} đã gặp {sourceTracker.ErrorCount} lỗi trong {TRACKING_WINDOW_MINUTES} phút",
                    ipAddress ?? "system",
                    AlertTypeId.SQLInjection,
                    SeverityLevelId.High);
            }
        }
    }

    public async Task RecordSuspiciousEventAsync(string eventType, string description, string ipAddress, string? userId = null)
    {
        using var scope = _scopeFactory.CreateScope();
        var logService = scope.ServiceProvider.GetRequiredService<ILogService>();
        var alertService = scope.ServiceProvider.GetRequiredService<IAlertService>();

        // Ghi log sự kiện đáng ngờ
        await logService.CreateLogAsync(new Log
        {
            Timestamp = DateTime.UtcNow,
            EventType = "Security",
            Message = $"Suspicious: {eventType}",
            RawData = description,
            IpAddress = ipAddress
        });

        // Tạo cảnh báo cho sự kiện đáng ngờ
        await CreateAlertAsync(alertService,
            $"Phát hiện hoạt động đáng ngờ: {eventType}",
            description,
            ipAddress,
            AlertTypeId.SuspiciousIP,
            SeverityLevelId.Medium);
    }

    private bool IsSensitiveEndpoint(string endpoint)
    {
        return SENSITIVE_ENDPOINTS.Any(sensitive => 
            endpoint.StartsWith(sensitive, StringComparison.OrdinalIgnoreCase));
    }

    private bool ContainsSuspiciousKeywords(string text)
    {
        return SUSPICIOUS_KEYWORDS.Any(keyword => 
            text.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<LogSource> EnsureLogSourceExistsAsync(ILogService logService, string name, string deviceType, string ipAddress)
    {
        var logSource = await logService.GetLogSourceByNameAsync(name);
        if (logSource == null)
        {
            logSource = new LogSource
            {
                Name = name,
                DeviceType = deviceType,
                IpAddress = ipAddress,
                Location = "System",
                IsActive = true,
                LastSeenAt = DateTime.UtcNow
            };
            await logService.CreateLogSourceAsync(logSource);
        }
        
        logSource.LastSeenAt = DateTime.UtcNow;
        await logService.UpdateLogSourceAsync(logSource);
        return logSource;
    }

    private async Task CreateAlertAsync(
        IAlertService alertService,
        string title,
        string description,
        string sourceIp,
        AlertTypeId alertType,
        SeverityLevelId severity)
    {
        var alert = new Alert
        {
            Title = title,
            Description = description,
            SourceIp = sourceIp,
            AlertTypeId = (int)alertType,
            SeverityLevelId = (int)severity,
            StatusId = (int)AlertStatusId.New,
            Timestamp = DateTime.UtcNow
        };

        await alertService.CreateAlertAsync(alert);
        await _hubContext.Clients.All.SendAsync("ReceiveAlert", new
        {
            alert.Id,
            alert.Title,
            alert.Description,
            alert.SourceIp,
            alert.Timestamp,
            SeverityLevel = severity.ToString(),
            Type = alertType.ToString()
        });

        _logger.LogWarning("🚨 {Title} từ IP: {IP}", title, sourceIp);
    }
}

public class UserActivityTracker
{
    private const int TRACKING_WINDOW_MINUTES = 5;
    
    public string UserId { get; }
    public int PasswordResetCount { get; private set; }
    public int EmailChangeCount { get; private set; }
    public DateTime LastActivityTime { get; private set; }
    private readonly List<DateTime> _activities;

    public UserActivityTracker(string userId)
    {
        UserId = userId;
        LastActivityTime = DateTime.UtcNow;
        _activities = new List<DateTime>();
        PasswordResetCount = 0;
        EmailChangeCount = 0;
    }

    public void RecordPasswordReset()
    {
        CleanupOldActivities();
        PasswordResetCount++;
        _activities.Add(DateTime.UtcNow);
        LastActivityTime = DateTime.UtcNow;
    }

    public void RecordEmailChange()
    {
        CleanupOldActivities();
        EmailChangeCount++;
        _activities.Add(DateTime.UtcNow);
        LastActivityTime = DateTime.UtcNow;
    }

    private void CleanupOldActivities()
    {
        var cutoff = DateTime.UtcNow.AddMinutes(-TRACKING_WINDOW_MINUTES);
        _activities.RemoveAll(t => t < cutoff);
        PasswordResetCount = 0;
        EmailChangeCount = 0;
    }
}

public class EndpointTracker
{
    private const int TRACKING_WINDOW_MINUTES = 5;
    
    public string Endpoint { get; }
    public int AccessCount { get; private set; }
    public int ErrorCount { get; private set; }
    public int NotFoundCount { get; private set; }
    public DateTime LastAccessTime { get; private set; }
    private readonly List<(DateTime time, string userId, string ipAddress, int statusCode)> _accesses;

    public EndpointTracker(string endpoint)
    {
        Endpoint = endpoint;
        LastAccessTime = DateTime.UtcNow;
        _accesses = new List<(DateTime, string, string, int)>();
        AccessCount = 0;
        ErrorCount = 0;
        NotFoundCount = 0;
    }

    public void RecordAccess(string userId, string ipAddress, int statusCode)
    {
        CleanupOldAccesses();
        AccessCount++;
        _accesses.Add((DateTime.UtcNow, userId, ipAddress, statusCode));
        LastAccessTime = DateTime.UtcNow;
    }

    public void RecordError()
    {
        CleanupOldAccesses();
        ErrorCount++;
        LastAccessTime = DateTime.UtcNow;
    }

    public void RecordNotFound()
    {
        CleanupOldAccesses();
        NotFoundCount++;
        LastAccessTime = DateTime.UtcNow;
    }

    private void CleanupOldAccesses()
    {
        var cutoff = DateTime.UtcNow.AddMinutes(-TRACKING_WINDOW_MINUTES);
        _accesses.RemoveAll(a => a.time < cutoff);
        AccessCount = _accesses.Count;
        ErrorCount = _accesses.Count(a => a.statusCode >= 500);
        NotFoundCount = _accesses.Count(a => a.statusCode == 404);
    }
}
