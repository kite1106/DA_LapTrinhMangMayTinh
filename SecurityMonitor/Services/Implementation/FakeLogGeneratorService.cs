using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.SignalR;
using SecurityMonitor.Models;
using SecurityMonitor.Services.Interfaces;
using SecurityMonitor.DTOs.Alerts;
using SecurityMonitor.Hubs;

namespace SecurityMonitor.Services.Implementation
{
    public class FakeLogGeneratorService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<FakeLogGeneratorService> _logger;
        private static int _counter = 1;
        private DateTime _lastAlertTime = DateTime.UtcNow;
        private readonly Random _random = new Random();

        public FakeLogGeneratorService(
            IServiceScopeFactory scopeFactory,
            ILogger<FakeLogGeneratorService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("🟢 FakeLogGeneratorService is running...");

            while (!stoppingToken.IsCancellationRequested)
            {
                using var scope = _scopeFactory.CreateScope();
                var logService = scope.ServiceProvider.GetRequiredService<ILogService>();
                var logControlService = scope.ServiceProvider.GetRequiredService<ILogGenerationControlService>();
                var logAnalysisService = scope.ServiceProvider.GetRequiredService<ILogAnalysisService>();

                // Kiểm tra xem log generation có được bật không
                var isEnabled = await logControlService.GetLogGenerationStatusAsync();
                if (!isEnabled)
                {
                    _logger.LogInformation("⏸️ Log generation is disabled, waiting...");
                    await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
                    continue;
                }

                // Tạo logs thực tế
                await CreateRealisticLogs(logService);

                // Phân tích logs để sinh cảnh báo (khoảng 10 phút 1 lần)
                if (logAnalysisService.ShouldAnalyzeLogs())
                {
                    await logAnalysisService.AnalyzeRecentLogsAndCreateAlertsAsync();
                }

                // Delay 1 giây trước khi tạo log tiếp theo
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            }
        }

        private async Task CreateRealisticLogs(ILogService logService)
        {
            // Tạo 3 logs mỗi giây
            var logCount = 1;
            
            for (int i = 0; i < logCount; i++)
            {
                string fakeIp = GetRealisticIP();
                var logData = GetRealisticLogData();
                
                var log = new LogEntry
                {
                    Timestamp = DateTime.UtcNow.AddSeconds(-_random.Next(0, 10)), // Thời gian ngẫu nhiên trong 10 giây qua
                    IpAddress = fakeIp,
                    Message = logData.Message,
                    LogSourceId = logData.SourceId,
                    LogLevelTypeId = logData.LevelId,
                    WasSuccessful = logData.IsSuccessful,
                    UserId = logData.UserId
                };

                var createdLog = await logService.CreateLogAsync(log);
                
                // Gửi log real-time qua SignalR (không có thông báo)
                try
                {
                    using var hubScope = _scopeFactory.CreateScope();
                    var logHub = hubScope.ServiceProvider.GetRequiredService<IHubContext<LogHub>>();
                    await logHub.Clients.Group("LogGroup").SendAsync("ReceiveNewLog", createdLog);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Không thể gửi log real-time: {Error}", ex.Message);
                }
            }
            
            // Không log thông báo để tránh spam
        }

        private (string Message, int SourceId, int LevelId, bool IsSuccessful, string UserId) GetRealisticLogData()
        {
            // Chọn ngẫu nhiên loại log source từ 6 nguồn
            var logSource = _random.Next(1, 7); // 1-6
            
            switch (logSource)
            {
                case 1: return GetApacheLogs();
                case 2: return GetNginxLogs();
                case 3: return GetWindowsEventLogs();
                case 4: return GetLinuxSyslogs();
                case 5: return GetMySQLLogs();
                case 6: return GetCustomAppLogs();
                default: return GetApacheLogs(); // Fallback
            }
        }

        private (string Message, int SourceId, int LevelId, bool IsSuccessful, string UserId) GetApacheLogs()
        {
            var ipAddress = $"192.168.1.{_random.Next(1, 255)}";
            var statusCode = _random.Next(100) > 90 ? _random.Next(400, 500) : 200;
            var responseSize = _random.Next(100, 10000);
            var methods = new[] { "GET", "POST", "PUT", "DELETE", "HEAD" };
            var method = methods[_random.Next(methods.Length)];
            var paths = new[] { "/api/users", "/admin/dashboard", "/api/alerts", "/api/logs", "/api/security-metrics", "/wp-admin", "/phpmyadmin", "/setup.php" };
            var path = paths[_random.Next(paths.Length)];

            // Thêm một số logs có pattern đáng ngờ để trigger cảnh báo
            var suspiciousPatterns = new[]
            {
                $"Failed login attempt for user admin from {ipAddress}",
                $"Multiple failed login attempts detected from {ipAddress}",
                $"Suspicious activity detected from {ipAddress} - {method} {path}",
                $"Unusual access pattern from {ipAddress} - {method} {path}",
                $"Anomaly detected: {ipAddress} accessing {path} repeatedly"
            };

            // 10% chance tạo log đáng ngờ
            if (_random.Next(100) < 10)
            {
                var suspiciousMessage = suspiciousPatterns[_random.Next(suspiciousPatterns.Length)];
                return (
                    suspiciousMessage,
                    1, // System
                    2, // Warning
                    false, // Failed
                    "Unknown"
                );
            }

            return (
                $"Apache Access: {ipAddress} - {method} {path} - {statusCode} - {responseSize} bytes",
                1, // System
                statusCode >= 400 ? 3 : 1, // Error or Info
                statusCode < 400,
                "Apache"
            );
        }

        private (string Message, int SourceId, int LevelId, bool IsSuccessful, string UserId) GetNginxLogs()
        {
            var ipAddress = $"10.0.0.{_random.Next(1, 255)}";
            var errorLevel = _random.Next(100) > 90 ? "error" : "warn";
            var errors = new[] { "upstream timed out", "connection refused", "permission denied", "file not found", "bad gateway" };
            var errorMessage = errors[_random.Next(errors.Length)];

            // Thêm một số logs có pattern đáng ngờ
            var suspiciousPatterns = new[]
            {
                $"Suspicious request from {ipAddress} - {errorMessage}",
                $"Multiple failed requests from {ipAddress}",
                $"Unusual traffic pattern from {ipAddress}",
                $"Anomaly detected: {ipAddress} causing {errorMessage}"
            };

            // 5% chance tạo log đáng ngờ
            if (_random.Next(100) < 5)
            {
                var suspiciousMessage = suspiciousPatterns[_random.Next(suspiciousPatterns.Length)];
                return (
                    suspiciousMessage,
                    2, // Network
                    2, // Warning
                    false, // Failed
                    "Unknown"
                );
            }

            return (
                $"Nginx {errorLevel.ToUpper()}: {errorMessage} from {ipAddress}",
                2, // Network
                errorLevel == "error" ? 3 : 2, // Error or Warning
                errorLevel == "warn",
                "Nginx"
            );
        }

        private (string Message, int SourceId, int LevelId, bool IsSuccessful, string UserId) GetWindowsEventLogs()
        {
            var eventTypes = new[] { "Security", "System", "System", "Application", "Application" };
            var eventType = eventTypes[_random.Next(eventTypes.Length)];
            var eventId = _random.Next(1000, 6000);
            var severity = _random.Next(100) > 60 ? "Information" : "Warning";
            var users = new[] { "SYSTEM", "Administrator", "LocalService", "NetworkService" };
            var user = users[_random.Next(users.Length)];

            return (
                $"Windows Event: {eventType} - Event ID: {eventId} - {severity}",
                3, // Application
                severity == "Warning" ? 2 : 1, // Warning or Info
                severity == "Information",
                user
            );
        }

        private (string Message, int SourceId, int LevelId, bool IsSuccessful, string UserId) GetLinuxSyslogs()
        {
            var services = new[] { "sshd", "cron", "cron", "systemd", "systemd", "kernel" };
            var service = services[_random.Next(services.Length)];
            var severity = _random.Next(100) > 90 ? "warning" : "info";
            var messages = new Dictionary<string, string[]>
            {
                ["sshd"] = new[] { "Accepted password", "Failed password", "Connection closed", "Authentication failure" },
                ["cron"] = new[] { "Job completed", "Job started", "Job failed", "Scheduled task executed" },
                ["systemd"] = new[] { "Service started", "Service stopped", "Service restarted", "Unit activated" },
                ["kernel"] = new[] { "CPU temperature high", "Memory usage high", "Disk space low", "Network interface up" }
            };

            var message = messages.ContainsKey(service) 
                ? messages[service][_random.Next(messages[service].Length)] 
                : "System event occurred";

            return (
                $"Syslog: {service} - {severity.ToUpper()} - {message}",
                4, // Database
                severity == "warning" ? 2 : 1, // Warning or Info
                severity == "info",
                "root"
            );
        }

        private (string Message, int SourceId, int LevelId, bool IsSuccessful, string UserId) GetMySQLLogs()
        {
            var errorTypes = new[] { "Connection timeout", "Query timeout", "Deadlock", "Access denied", "Table not found" };
            var errorType = errorTypes[_random.Next(errorTypes.Length)];
            var severity = _random.Next(100) > 90 ? "Error" : "Warning";
            var ipAddress = $"10.0.0.{_random.Next(1, 255)}";

            return (
                $"MySQL {severity}: {errorType} from {ipAddress}",
                5, // Database
                severity == "Error" ? 3 : 2, // Error or Warning
                severity == "Warning",
                "mysql"
            );
        }

        private (string Message, int SourceId, int LevelId, bool IsSuccessful, string UserId) GetCustomAppLogs()
        {
            var users = new[] { "admin", "user1", "service", "guest" };
            var user = users[_random.Next(users.Length)];
            var actions = new[] { "login", "logout", "file_access", "data_export", "config_change", "password_change" };
            var action = actions[_random.Next(actions.Length)];
            var ipAddress = $"172.16.{_random.Next(1, 255)}.{_random.Next(1, 255)}";
            var success = _random.Next(100) > 90;

            // Thêm một số logs có pattern đáng ngờ
            var suspiciousPatterns = new[]
            {
                $"Failed login attempt for user {user} from {ipAddress}",
                $"Multiple failed login attempts for {user} from {ipAddress}",
                $"Suspicious activity detected from {ipAddress} - {action}",
                $"Unusual access pattern from {ipAddress} - {action}",
                $"Anomaly detected: {ipAddress} performing {action} repeatedly"
            };

            // 8% chance tạo log đáng ngờ
            if (_random.Next(100) < 8)
            {
                var suspiciousMessage = suspiciousPatterns[_random.Next(suspiciousPatterns.Length)];
                return (
                    suspiciousMessage,
                    1, // Custom App
                    2, // Warning
                    false, // Failed
                    "Unknown"
                );
            }

            return (
                $"Custom App: {user} {action} - {(success ? "SUCCESS" : "FAILED")} from {ipAddress}",
                1, // System
                success ? 1 : 3, // Info or Error
                success,
                user
            );
        }

        private string GetRealisticIP()
        {
            var ipRanges = new[]
            {
                "192.168.1.{0}",
                "172.16.0.{0}",
            };
            
            var selectedRange = ipRanges[_random.Next(ipRanges.Length)];
            return string.Format(selectedRange, _random.Next(1, 255));
        }


    }
}
