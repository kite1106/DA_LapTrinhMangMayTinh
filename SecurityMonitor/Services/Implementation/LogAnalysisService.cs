
using SecurityMonitor.Data;
using SecurityMonitor.Models;
using SecurityMonitor.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SecurityMonitor.DTOs.Alerts;
using Microsoft.Extensions.DependencyInjection;

namespace SecurityMonitor.Services.Implementation
{
    public class LogAnalysisService : ILogAnalysisService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<LogAnalysisService> _logger;
        private DateTime _lastAnalysisTime = DateTime.UtcNow;
        private readonly TimeSpan _analysisInterval = TimeSpan.FromSeconds(30); // Phân tích mỗi 30 giây

        public LogAnalysisService(
            IServiceScopeFactory scopeFactory,
            ILogger<LogAnalysisService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        public bool ShouldAnalyzeLogs()
        {
            // Phân tích logs mỗi 30 giây
            return DateTime.UtcNow - _lastAnalysisTime >= _analysisInterval;
        }

        public async Task AnalyzeRecentLogsAndCreateAlertsAsync()
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var alertService = scope.ServiceProvider.GetRequiredService<IAlertService>();

            try
            {
                // Lấy logs trong 5 phút gần đây
                var recentLogs = await context.LogEntries
                    .Where(l => l.Timestamp >= DateTime.UtcNow.AddMinutes(-5))
                    .OrderByDescending(l => l.Timestamp)
                    .ToListAsync();

                if (!recentLogs.Any())
                {
                    _logger.LogInformation("📊 Không có logs gần đây để phân tích");
                    return;
                }

                _logger.LogInformation("🔍 Phân tích {Count} logs gần đây", recentLogs.Count);

                // Phân tích các pattern đáng ngờ
                await AnalyzeFailedLoginPatterns(recentLogs, alertService);
                await AnalyzeSuspiciousActivityPatterns(recentLogs, alertService);
                await AnalyzeErrorRatePatterns(recentLogs, alertService);
                await AnalyzeUnusualAccessPatterns(recentLogs, alertService);
                
                // Phân tích theo từng nguồn logs
                await AnalyzeApacheLogs(recentLogs, alertService);
                await AnalyzeNginxLogs(recentLogs, alertService);
                await AnalyzeWindowsLogs(recentLogs, alertService);
                await AnalyzeLinuxLogs(recentLogs, alertService);
                await AnalyzeMySQLLogs(recentLogs, alertService);
                await AnalyzeCustomAppLogs(recentLogs, alertService);
                
                // Phân tích hoạt động user đáng ngờ
                await AnalyzeUserPasswordChangePatterns(recentLogs, alertService);
                await AnalyzeUserEmailChangePatterns(recentLogs, alertService);

                _lastAnalysisTime = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Lỗi khi phân tích logs");
            }
        }

        private async Task AnalyzeFailedLoginPatterns(List<LogEntry> logs, IAlertService alertService)
        {
            // Tìm các IP có nhiều lần đăng nhập thất bại
            var failedLogins = logs.Where(l => 
                (l.Message.Contains("login") && l.Message.Contains("failed", StringComparison.OrdinalIgnoreCase)) ||
                (l.Message.Contains("Failed login attempt")) ||
                (l.Message.Contains("Multiple failed login attempts")) &&
                !l.WasSuccessful)
                .GroupBy(l => l.IpAddress)
                .Where(g => g.Count() >= 2) // Giảm xuống 2 lần thất bại
                .ToList();

            foreach (var group in failedLogins)
            {
                var alert = new Alert
                {
                    Title = "Phát hiện tấn công brute force",
                    Description = $"IP {group.Key} có {group.Count()} lần đăng nhập thất bại trong 5 phút qua",
                    SourceIp = group.Key,
                    AlertTypeId = (int)AlertTypeId.BruteForce,
                    SeverityLevelId = (int)SeverityLevelId.High,
                    StatusId = (int)AlertStatusId.New,
                    Timestamp = DateTime.UtcNow
                };

                await alertService.CreateAlertAsync(alert);
                _logger.LogWarning("🚨 Phát hiện brute force từ IP: {IP}", group.Key);
            }
        }

        private async Task AnalyzeSuspiciousActivityPatterns(List<LogEntry> logs, IAlertService alertService)
        {
            // Tìm các hoạt động đáng ngờ
            var suspiciousLogs = logs.Where(l => 
                l.Message.Contains("suspicious", StringComparison.OrdinalIgnoreCase) ||
                l.Message.Contains("unusual", StringComparison.OrdinalIgnoreCase) ||
                l.Message.Contains("anomaly", StringComparison.OrdinalIgnoreCase) ||
                l.Message.Contains("Suspicious activity detected") ||
                l.Message.Contains("Unusual access pattern") ||
                l.Message.Contains("Anomaly detected"))
                .GroupBy(l => l.IpAddress)
                .ToList();

            foreach (var group in suspiciousLogs)
            {
                var alert = new Alert
                {
                    Title = "Phát hiện hoạt động đáng ngờ",
                    Description = $"IP {group.Key} có {group.Count()} hoạt động đáng ngờ",
                    SourceIp = group.Key,
                    AlertTypeId = (int)AlertTypeId.SuspiciousIP,
                    SeverityLevelId = (int)SeverityLevelId.Medium,
                    StatusId = (int)AlertStatusId.New,
                    Timestamp = DateTime.UtcNow
                };

                await alertService.CreateAlertAsync(alert);
                _logger.LogWarning("🚨 Phát hiện hoạt động đáng ngờ từ IP: {IP}", group.Key);
            }
        }

        private async Task AnalyzeErrorRatePatterns(List<LogEntry> logs, IAlertService alertService)
        {
            // Tính tỷ lệ lỗi
            var totalLogs = logs.Count;
            var errorLogs = logs.Where(l => l.LogLevelTypeId == 3 || !l.WasSuccessful).Count();
            
            if (totalLogs > 0 && (double)errorLogs / totalLogs > 0.3) // 30% lỗi trở lên
            {
                var alert = new Alert
                {
                    Title = "Tỷ lệ lỗi cao bất thường",
                    Description = $"Tỷ lệ lỗi: {errorLogs}/{totalLogs} ({((double)errorLogs/totalLogs*100):F1}%)",
                    SourceIp = "System",
                    AlertTypeId = (int)AlertTypeId.SuspiciousIP,
                    SeverityLevelId = (int)SeverityLevelId.High,
                    StatusId = (int)AlertStatusId.New,
                    Timestamp = DateTime.UtcNow
                };

                await alertService.CreateAlertAsync(alert);
                _logger.LogWarning("🚨 Phát hiện tỷ lệ lỗi cao: {ErrorRate}%", ((double)errorLogs/totalLogs*100));
            }
        }

        private async Task AnalyzeUnusualAccessPatterns(List<LogEntry> logs, IAlertService alertService)
        {
            // Tìm các IP truy cập quá nhiều endpoint khác nhau từ TẤT CẢ nguồn logs
            var accessPatterns = logs
                .GroupBy(l => l.IpAddress)
                .Where(g => g.Select(l => l.Message).Distinct().Count() > 3) // Giảm xuống 3 endpoint khác nhau
                .ToList();

            foreach (var group in accessPatterns)
            {
                var uniqueEndpoints = group.Select(l => l.Message).Distinct().Count();
                var alert = new Alert
                {
                    Title = "Phát hiện truy cập bất thường",
                    Description = $"IP {group.Key} truy cập {uniqueEndpoints} endpoint khác nhau từ nhiều nguồn trong 5 phút",
                    SourceIp = group.Key,
                    AlertTypeId = (int)AlertTypeId.SuspiciousIP,
                    SeverityLevelId = (int)SeverityLevelId.Medium,
                    StatusId = (int)AlertStatusId.New,
                    Timestamp = DateTime.UtcNow
                };

                await alertService.CreateAlertAsync(alert);
                _logger.LogWarning("🚨 Phát hiện truy cập bất thường từ IP: {IP}", group.Key);
            }
        }

        private async Task AnalyzeApacheLogs(List<LogEntry> logs, IAlertService alertService)
        {
            // Phân tích Apache logs (LogSourceId = 1)
            var apacheLogs = logs.Where(l => l.LogSourceId == 1).ToList();
            
            if (!apacheLogs.Any()) return;

            // Tìm các IP có nhiều request lỗi 4xx/5xx
            var errorRequests = apacheLogs
                .Where(l => l.Message.Contains("4") || l.Message.Contains("5"))
                .GroupBy(l => l.IpAddress)
                .Where(g => g.Count() >= 2)
                .ToList();

            foreach (var group in errorRequests)
            {
                var alert = new Alert
                {
                    Title = "Phát hiện nhiều request lỗi từ Apache",
                    Description = $"IP {group.Key} có {group.Count()} request lỗi từ Apache trong 5 phút",
                    SourceIp = group.Key,
                    AlertTypeId = (int)AlertTypeId.SuspiciousIP,
                    SeverityLevelId = (int)SeverityLevelId.Medium,
                    StatusId = (int)AlertStatusId.New,
                    Timestamp = DateTime.UtcNow
                };

                await alertService.CreateAlertAsync(alert);
                _logger.LogWarning("🚨 Phát hiện nhiều request lỗi Apache từ IP: {IP}", group.Key);
            }
        }

        private async Task AnalyzeNginxLogs(List<LogEntry> logs, IAlertService alertService)
        {
            // Phân tích Nginx logs (LogSourceId = 2)
            var nginxLogs = logs.Where(l => l.LogSourceId == 2).ToList();
            
            if (!nginxLogs.Any()) return;

            // Tìm các IP có nhiều lỗi nginx
            var errorLogs = nginxLogs
                .Where(l => !l.WasSuccessful)
                .GroupBy(l => l.IpAddress)
                .Where(g => g.Count() >= 2)
                .ToList();

            foreach (var group in errorLogs)
            {
                var alert = new Alert
                {
                    Title = "Phát hiện nhiều lỗi Nginx",
                    Description = $"IP {group.Key} có {group.Count()} lỗi Nginx trong 5 phút",
                    SourceIp = group.Key,
                    AlertTypeId = (int)AlertTypeId.SuspiciousIP,
                    SeverityLevelId = (int)SeverityLevelId.Medium,
                    StatusId = (int)AlertStatusId.New,
                    Timestamp = DateTime.UtcNow
                };

                await alertService.CreateAlertAsync(alert);
                _logger.LogWarning("🚨 Phát hiện nhiều lỗi Nginx từ IP: {IP}", group.Key);
            }
        }

        private async Task AnalyzeWindowsLogs(List<LogEntry> logs, IAlertService alertService)
        {
            // Phân tích Windows logs (LogSourceId = 3)
            var windowsLogs = logs.Where(l => l.LogSourceId == 3).ToList();
            
            if (!windowsLogs.Any()) return;

            // Tìm các event bảo mật đáng ngờ
            var securityEvents = windowsLogs
                .Where(l => l.Message.Contains("Security") && !l.WasSuccessful)
                .GroupBy(l => l.IpAddress)
                .Where(g => g.Count() >= 1)
                .ToList();

            foreach (var group in securityEvents)
            {
                var alert = new Alert
                {
                    Title = "Phát hiện event bảo mật Windows",
                    Description = $"IP {group.Key} có {group.Count()} event bảo mật Windows trong 5 phút",
                    SourceIp = group.Key,
                    AlertTypeId = (int)AlertTypeId.SuspiciousIP,
                    SeverityLevelId = (int)SeverityLevelId.High,
                    StatusId = (int)AlertStatusId.New,
                    Timestamp = DateTime.UtcNow
                };

                await alertService.CreateAlertAsync(alert);
                _logger.LogWarning("🚨 Phát hiện event bảo mật Windows từ IP: {IP}", group.Key);
            }
        }

        private async Task AnalyzeLinuxLogs(List<LogEntry> logs, IAlertService alertService)
        {
            // Phân tích Linux logs (LogSourceId = 4)
            var linuxLogs = logs.Where(l => l.LogSourceId == 4).ToList();
            
            if (!linuxLogs.Any()) return;

            // Tìm các lỗi SSH đáng ngờ
            var sshErrors = linuxLogs
                .Where(l => l.Message.Contains("sshd") && l.Message.Contains("Failed"))
                .GroupBy(l => l.IpAddress)
                .Where(g => g.Count() >= 2)
                .ToList();

            foreach (var group in sshErrors)
            {
                var alert = new Alert
                {
                    Title = "Phát hiện tấn công SSH",
                    Description = $"IP {group.Key} có {group.Count()} lần thất bại SSH trong 5 phút",
                    SourceIp = group.Key,
                    AlertTypeId = (int)AlertTypeId.BruteForce,
                    SeverityLevelId = (int)SeverityLevelId.High,
                    StatusId = (int)AlertStatusId.New,
                    Timestamp = DateTime.UtcNow
                };

                await alertService.CreateAlertAsync(alert);
                _logger.LogWarning("🚨 Phát hiện tấn công SSH từ IP: {IP}", group.Key);
            }
        }

        private async Task AnalyzeMySQLLogs(List<LogEntry> logs, IAlertService alertService)
        {
            // Phân tích MySQL logs (LogSourceId = 5)
            var mysqlLogs = logs.Where(l => l.LogSourceId == 5).ToList();
            
            if (!mysqlLogs.Any()) return;

            // Tìm các lỗi database đáng ngờ
            var dbErrors = mysqlLogs
                .Where(l => !l.WasSuccessful)
                .GroupBy(l => l.IpAddress)
                .Where(g => g.Count() >= 2)
                .ToList();

            foreach (var group in dbErrors)
            {
                var alert = new Alert
                {
                    Title = "Phát hiện nhiều lỗi MySQL",
                    Description = $"IP {group.Key} có {group.Count()} lỗi MySQL trong 5 phút",
                    SourceIp = group.Key,
                    AlertTypeId = (int)AlertTypeId.SuspiciousIP,
                    SeverityLevelId = (int)SeverityLevelId.Medium,
                    StatusId = (int)AlertStatusId.New,
                    Timestamp = DateTime.UtcNow
                };

                await alertService.CreateAlertAsync(alert);
                _logger.LogWarning("🚨 Phát hiện nhiều lỗi MySQL từ IP: {IP}", group.Key);
            }
        }

        private async Task AnalyzeCustomAppLogs(List<LogEntry> logs, IAlertService alertService)
        {
            // Phân tích Custom App logs (LogSourceId = 1 nhưng có pattern khác)
            var customAppLogs = logs.Where(l => l.LogSourceId == 1 && l.UserId != "Apache").ToList();
            
            if (!customAppLogs.Any()) return;

            // Tìm các hoạt động đáng ngờ từ custom app
            var suspiciousActions = customAppLogs
                .Where(l => l.Message.Contains("FAILED") || !l.WasSuccessful)
                .GroupBy(l => l.IpAddress)
                .Where(g => g.Count() >= 2)
                .ToList();

            foreach (var group in suspiciousActions)
            {
                var alert = new Alert
                {
                    Title = "Phát hiện hoạt động đáng ngờ từ Custom App",
                    Description = $"IP {group.Key} có {group.Count()} hoạt động thất bại từ Custom App trong 5 phút",
                    SourceIp = group.Key,
                    AlertTypeId = (int)AlertTypeId.SuspiciousIP,
                    SeverityLevelId = (int)SeverityLevelId.Medium,
                    StatusId = (int)AlertStatusId.New,
                    Timestamp = DateTime.UtcNow
                };

                await alertService.CreateAlertAsync(alert);
                _logger.LogWarning("🚨 Phát hiện hoạt động đáng ngờ từ Custom App từ IP: {IP}", group.Key);
            }
        }

        private async Task AnalyzeUserPasswordChangePatterns(List<LogEntry> logs, IAlertService alertService)
        {
            // Phân tích user đổi password nhiều lần
            var passwordChangeLogs = logs
                .Where(l => l.Message.Contains("changed password") && !string.IsNullOrEmpty(l.UserId))
                .GroupBy(l => l.UserId)
                .Where(g => g.Count() >= 3) // 3 lần đổi password trong 5 phút
                .ToList();

            foreach (var group in passwordChangeLogs)
            {
                var alert = new Alert
                {
                    Title = "Phát hiện đổi password nhiều lần",
                    Description = $"User {group.Key} đã đổi password {group.Count()} lần trong 5 phút qua",
                    SourceIp = group.First().IpAddress,
                    AlertTypeId = (int)AlertTypeId.SuspiciousIP,
                    SeverityLevelId = (int)SeverityLevelId.High,
                    StatusId = (int)AlertStatusId.New,
                    Timestamp = DateTime.UtcNow
                };

                await alertService.CreateAlertAsync(alert);
                _logger.LogWarning("🚨 Phát hiện user đổi password nhiều lần: {UserId}", group.Key);
            }
        }

        private async Task AnalyzeUserEmailChangePatterns(List<LogEntry> logs, IAlertService alertService)
        {
            // Phân tích user đổi email nhiều lần
            var emailChangeLogs = logs
                .Where(l => l.Message.Contains("requested email change") && !string.IsNullOrEmpty(l.UserId))
                .GroupBy(l => l.UserId)
                .Where(g => g.Count() >= 2) // 2 lần đổi email trong 5 phút
                .ToList();

            foreach (var group in emailChangeLogs)
            {
                var alert = new Alert
                {
                    Title = "Phát hiện đổi email nhiều lần",
                    Description = $"User {group.Key} đã yêu cầu đổi email {group.Count()} lần trong 5 phút qua",
                    SourceIp = group.First().IpAddress,
                    AlertTypeId = (int)AlertTypeId.SuspiciousIP,
                    SeverityLevelId = (int)SeverityLevelId.High,
                    StatusId = (int)AlertStatusId.New,
                    Timestamp = DateTime.UtcNow
                };

                await alertService.CreateAlertAsync(alert);
                _logger.LogWarning("🚨 Phát hiện user đổi email nhiều lần: {UserId}", group.Key);
            }
        }
    }
} 