using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SecurityMonitor.Hubs;
using SecurityMonitor.Models;
using SecurityMonitor.Services.Interfaces;
using System.Collections.Concurrent;

namespace SecurityMonitor.Services.Implementation;

public class LogAnalyzerService : ILogAnalyzerService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LogAnalyzerService> _logger;
    private readonly IHubContext<AlertHub> _hubContext;
    private readonly ConcurrentDictionary<string, RequestTracker> _requestTrackers;

    // Các ngưỡng cảnh báo cho môi trường test
    private const int DDOS_THRESHOLD = 20; // Số request/phút để coi là DDoS (giảm từ 100 xuống 20)
    private const int ERROR_THRESHOLD = 3; // Số lỗi 500/phút để cảnh báo (giảm từ 10 xuống 3)
    private const int AUTH_FAILURE_THRESHOLD = 3; // Số lần auth fail/phút (giảm từ 5 xuống 3)
    private const int MONITORING_WINDOW_MINUTES = 2; // Thời gian theo dõi (tăng từ 1 lên 2 phút cho dễ test)

    private static readonly string[] SENSITIVE_ENDPOINTS = new[]
    {
        "/admin", "/config", "/api/secret", "/settings", "/system"
    };

    private static readonly string[] SECURITY_KEYWORDS = new[]
    {
        "unauthorized", "forbidden", "invalid token", "csrf", "xss", "injection"
    };

    public LogAnalyzerService(
        IServiceScopeFactory scopeFactory,
        IHubContext<AlertHub> hubContext,
        ILogger<LogAnalyzerService> logger)
    {
        _scopeFactory = scopeFactory;
        _hubContext = hubContext;
        _logger = logger;
        _requestTrackers = new ConcurrentDictionary<string, RequestTracker>();
    }

    public async Task AnalyzeRequestAsync(string ipAddress, string endpoint, int statusCode, string userId)
    {
        var now = DateTime.UtcNow;
        var tracker = _requestTrackers.GetOrAdd(ipAddress, _ => new RequestTracker(ipAddress));
        tracker.RecordRequest(endpoint, statusCode);

        using var scope = _scopeFactory.CreateScope();
        var logService = scope.ServiceProvider.GetRequiredService<ILogService>();
        var sourceService = scope.ServiceProvider.GetRequiredService<ILogSourceService>();
        var alertService = scope.ServiceProvider.GetRequiredService<IAlertService>();

        // Lấy hoặc tạo LogSource cho web server
        var webServer = await GetOrCreateLogSource(sourceService);

        // 1. Log mọi request
        await logService.CreateLogAsync(new Log
        {
            Timestamp = now,
            LogSourceId = webServer.Id,
            EventType = "WebRequest",
            Message = $"Request to {endpoint}",
            RawData = $"Status: {statusCode}, User: {userId}",
            IpAddress = ipAddress,
            ProcessedAt = now
        });

        // 2. Kiểm tra truy cập endpoint nhạy cảm
        if (IsSensitiveEndpoint(endpoint))
        {
            await logService.CreateLogAsync(new Log
            {
                Timestamp = now,
                LogSourceId = webServer.Id,
                EventType = "Security",
                Message = $"Sensitive endpoint access: {endpoint}",
                RawData = $"IP: {ipAddress}, User: {userId}",
                IpAddress = ipAddress,
                ProcessedAt = now
            });

            if (statusCode == 403 || statusCode == 401)
            {
                await CreateAlertAsync(alertService, "Truy cập trái phép vào endpoint nhạy cảm",
                    $"IP {ipAddress} cố gắng truy cập {endpoint} không được phép",
                    ipAddress, AlertTypeId.SuspiciousIP, SeverityLevelId.High);
            }
        }

        // 3. Phát hiện DDoS
        if (tracker.IsExceedingRateLimit(DDOS_THRESHOLD, TimeSpan.FromMinutes(MONITORING_WINDOW_MINUTES)))
        {
            await CreateAlertAsync(alertService, "Phát hiện dấu hiệu DDoS",
                $"IP {ipAddress} gửi {tracker.RequestCount} requests trong {MONITORING_WINDOW_MINUTES} phút",
                ipAddress, AlertTypeId.DDoS, SeverityLevelId.Critical);
        }

        // 4. Phát hiện lỗi hệ thống
        if (statusCode >= 500)
        {
            tracker.RecordError();
            if (tracker.ErrorCount >= ERROR_THRESHOLD)
            {
                await CreateAlertAsync(alertService, "Phát hiện nhiều lỗi hệ thống",
                    $"Có {tracker.ErrorCount} lỗi server trong {MONITORING_WINDOW_MINUTES} phút từ IP {ipAddress}",
                    ipAddress, AlertTypeId.SQLInjection, SeverityLevelId.High);
            }
        }

        // 5. Kiểm tra authentication failures
        if (statusCode == 401 || statusCode == 403)
        {
            tracker.RecordAuthFailure();
            if (tracker.AuthFailureCount >= AUTH_FAILURE_THRESHOLD)
            {
                await CreateAlertAsync(alertService, "Phát hiện nhiều lần xác thực thất bại",
                    $"IP {ipAddress} có {tracker.AuthFailureCount} lần xác thực thất bại trong {MONITORING_WINDOW_MINUTES} phút",
                    ipAddress, AlertTypeId.BruteForce, SeverityLevelId.High);
            }
        }

        // Dọn dẹp trackers cũ
        CleanupOldTrackers();
    }

    private bool IsSensitiveEndpoint(string endpoint)
    {
        return SENSITIVE_ENDPOINTS.Any(sensitive => 
            endpoint.StartsWith(sensitive, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<LogSource> GetOrCreateLogSource(ILogSourceService sourceService)
    {
        var webServer = await sourceService.GetLogSourceByNameAsync("Web Server");
        if (webServer == null)
        {
            webServer = await sourceService.CreateLogSourceAsync(new LogSource
            {
                Name = "Web Server",
                DeviceType = "Web Server",
                IpAddress = "127.0.0.1",
                Location = "Local",
                IsActive = true
            });
        }
        return webServer;
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

    private void CleanupOldTrackers()
    {
        var cutoff = DateTime.UtcNow.AddMinutes(-MONITORING_WINDOW_MINUTES);
        var oldKeys = _requestTrackers
            .Where(kvp => kvp.Value.LastRequestTime < cutoff)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in oldKeys)
        {
            _requestTrackers.TryRemove(key, out _);
        }
    }
}

public class RequestTracker
{
    public string IpAddress { get; }
    public int RequestCount { get; private set; }
    public int ErrorCount { get; private set; }
    public int AuthFailureCount { get; private set; }
    public DateTime FirstRequestTime { get; }
    public DateTime LastRequestTime { get; private set; }
    private readonly List<DateTime> _requestTimestamps;

    public RequestTracker(string ipAddress)
    {
        IpAddress = ipAddress;
        FirstRequestTime = DateTime.UtcNow;
        LastRequestTime = DateTime.UtcNow;
        RequestCount = 0;
        ErrorCount = 0;
        AuthFailureCount = 0;
        _requestTimestamps = new List<DateTime>();
    }

    public void RecordRequest(string endpoint, int statusCode)
    {
        RequestCount++;
        LastRequestTime = DateTime.UtcNow;
        _requestTimestamps.Add(LastRequestTime);
    }

    public void RecordError()
    {
        ErrorCount++;
    }

    public void RecordAuthFailure()
    {
        AuthFailureCount++;
    }

    public bool IsExceedingRateLimit(int threshold, TimeSpan window)
    {
        var cutoff = DateTime.UtcNow - window;
        _requestTimestamps.RemoveAll(t => t < cutoff);
        return _requestTimestamps.Count >= threshold;
    }
}
