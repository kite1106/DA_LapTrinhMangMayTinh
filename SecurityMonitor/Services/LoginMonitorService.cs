using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SecurityMonitor.Hubs;
using SecurityMonitor.Models;
using SecurityMonitor.Services.Interfaces;
using SecurityMonitor.Services.Implementation;
using System.Collections.Concurrent;

namespace SecurityMonitor.Services;

public class LoginMonitorService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LoginMonitorService> _logger;
    private readonly IHubContext<AlertHub> _hubContext;
    private readonly ConcurrentDictionary<string, LoginAttempt> _loginAttempts;

    // Cấu hình ngưỡng cảnh báo
    private const int MAX_FAILED_ATTEMPTS = 5; // Số lần thất bại tối đa
    private const int MONITORING_WINDOW_MINUTES = 3; // Thời gian theo dõi (3 phút)
    
    public LoginMonitorService(
        IServiceScopeFactory scopeFactory,
        IHubContext<AlertHub> hubContext,
        ILogger<LoginMonitorService> logger)
    {
        _scopeFactory = scopeFactory;
        _hubContext = hubContext;
        _logger = logger;
        _loginAttempts = new ConcurrentDictionary<string, LoginAttempt>();
    }

    // Ghi nhận một lần đăng nhập
    public async Task RecordLoginAttemptAsync(string ipAddress, bool isSuccessful, string? username = null)
    {
        var now = DateTime.UtcNow;
        var attempt = _loginAttempts.GetOrAdd(ipAddress, _ => new LoginAttempt(ipAddress));

        // Cập nhật thông tin login attempt
        attempt.RecordAttempt(isSuccessful);

        // Lưu log cho mỗi lần đăng nhập thất bại
        if (!isSuccessful)
        {
            using var scope = _scopeFactory.CreateScope();
            var logService = scope.ServiceProvider.GetRequiredService<ILogService>();
            var sourceService = scope.ServiceProvider.GetRequiredService<ILogSourceService>();

            // Lấy LogSource cho authentication server
            var authServer = await sourceService.GetLogSourceByNameAsync("Windows Server 2022");
            if (authServer == null)
            {
                // Nếu chưa có, tạo mới LogSource cho auth server
                authServer = await sourceService.CreateLogSourceAsync(new LogSource
                {
                    Name = "Windows Server 2022",
                    DeviceType = "Authentication Server",
                    IpAddress = "127.0.0.1",
                    Location = "Local",
                    IsActive = true
                });
            }

            // Tạo log cho lần đăng nhập thất bại
            await logService.CreateLogAsync(new LogEntry
            {
                Timestamp = now,
                LogSourceId = authServer.Id,
                Message = $"Failed login attempt for user '{username ?? "unknown"}' from IP {ipAddress}",
                Details = $"Failed login attempt from IP {ipAddress}",
                IpAddress = ipAddress,
                ProcessedAt = now
            });

            // Kiểm tra nếu vượt ngưỡng cảnh báo
            if (ShouldCreateAlert(attempt))
            {
                var alertService = scope.ServiceProvider.GetRequiredService<IAlertService>();

                var alert = new Alert
                {
                    Title = "Phát hiện nhiều lần đăng nhập thất bại",
                    Description = $"IP {ipAddress} đã có {attempt.FailedAttempts} lần đăng nhập thất bại trong {MONITORING_WINDOW_MINUTES} phút qua.",
                    SourceIp = ipAddress,
                    AlertTypeId = (int)AlertTypeId.BruteForce,
                    SeverityLevelId = (int)SeverityLevelId.High,
                    StatusId = (int)AlertStatusId.New,
                    Timestamp = now
                };

                // Lưu cảnh báo vào database
                await alertService.CreateAlertAsync(alert);

                // Gửi cảnh báo realtime qua SignalR
                await _hubContext.Clients.All.SendAsync("ReceiveAlert", new
                {
                    alert.Id,
                    alert.Title,
                    alert.Description,
                    alert.SourceIp,
                    alert.Timestamp,
                    SeverityLevel = "High",
                    Type = "Brute Force Attempt"
                });

                _logger.LogWarning("🚨 Phát hiện nhiều lần đăng nhập thất bại từ IP: {IP}", ipAddress);
            }

            // Xóa các bản ghi cũ
            CleanupOldAttempts();
        }
    }
    private bool ShouldCreateAlert(LoginAttempt attempt)
    {
        return attempt.FailedAttempts >= MAX_FAILED_ATTEMPTS &&
               !attempt.HasAlertCreated &&
               (DateTime.UtcNow - attempt.FirstAttemptTime).TotalMinutes <= MONITORING_WINDOW_MINUTES;
    }

    private void CleanupOldAttempts()
    {
        var cutoff = DateTime.UtcNow.AddMinutes(-MONITORING_WINDOW_MINUTES);
        var oldKeys = _loginAttempts.Where(kvp => kvp.Value.LastAttemptTime < cutoff)
                                  .Select(kvp => kvp.Key)
                                  .ToList();

        foreach (var key in oldKeys)
        {
            _loginAttempts.TryRemove(key, out _);
        }
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Service chỉ xử lý các sự kiện được gọi từ bên ngoài
        return Task.CompletedTask;
    }
}

public class LoginAttempt
{
    public string IpAddress { get; }
    public int TotalAttempts { get; private set; }
    public int FailedAttempts { get; private set; }
    public DateTime FirstAttemptTime { get; }
    public DateTime LastAttemptTime { get; private set; }
    public bool HasAlertCreated { get; private set; }

    public LoginAttempt(string ipAddress)
    {
        IpAddress = ipAddress;
        FirstAttemptTime = DateTime.UtcNow;
        LastAttemptTime = DateTime.UtcNow;
        TotalAttempts = 0;
        FailedAttempts = 0;
        HasAlertCreated = false;
    }

    public void RecordAttempt(bool isSuccessful)
    {
        TotalAttempts++;
        if (!isSuccessful)
        {
            FailedAttempts++;
        }
        LastAttemptTime = DateTime.UtcNow;
    }

    public void MarkAlertCreated()
    {
        HasAlertCreated = true;
    }
}
