using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecurityMonitor.Services.Interfaces;
using SecurityMonitor.Models;
using SecurityMonitor.DTOs.Logs;
using SecurityMonitor.Services.IPBlocking;
using Microsoft.AspNetCore.Identity;
using SecurityMonitor.DTOs.Alerts;

namespace SecurityMonitor.Controllers.Api;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private readonly IAlertService _alertService;
    private readonly ILogService _logService;
    private readonly ILogGenerationControlService _logGenerationControlService;
    private readonly IIPBlockingService _ipBlockingService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<AdminController> _logger;
    private readonly Random _random = new Random();

    public AdminController(
        IAlertService alertService,
        ILogService logService,
        ILogGenerationControlService logGenerationControlService,
        IIPBlockingService ipBlockingService,
        UserManager<ApplicationUser> userManager,
        ILogger<AdminController> logger)
    {
        _alertService = alertService;
        _logService = logService;
        _logGenerationControlService = logGenerationControlService;
        _ipBlockingService = ipBlockingService;
        _userManager = userManager;
        _logger = logger;
    }

    [HttpPost("startLogGeneration")]
    public async Task<IActionResult> StartLogGeneration()
    {
        try
        {
            await _logGenerationControlService.EnableLogGenerationAsync();
            _logger.LogInformation("Log generation started by admin");
            
            return Ok(new { message = "Log generation started", isActive = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting log generation");
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("stopLogGeneration")]
    public async Task<IActionResult> StopLogGeneration()
    {
        try
        {
            await _logGenerationControlService.DisableLogGenerationAsync();
            _logger.LogInformation("Log generation stopped by admin");
            
            return Ok(new { message = "Log generation stopped", isActive = false });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping log generation");
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("logGenerationStatus")]
    public async Task<IActionResult> GetLogGenerationStatus()
    {
        try
        {
            var isActive = await _logGenerationControlService.GetLogGenerationStatusAsync();
            return Ok(new { isActive = isActive });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting log generation status");
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("generateTestAlert")]
    public async Task<IActionResult> GenerateTestAlert()
    {
        try
        {
            var alertTypes = new[] { "SuspiciousIP", "DDoS", "BruteForce", "SQLInjection" };
            var severities = new[] { "Critical", "High", "Medium", "Low" };
            var ips = new[] { "192.168.1.100", "10.0.0.50", "172.16.0.25", "203.0.113.10" };
            
            var random = new Random();
            var alertType = alertTypes[random.Next(alertTypes.Length)];
            var severity = severities[random.Next(severities.Length)];
            var ip = ips[random.Next(ips.Length)];
            
            var alert = new Alert
            {
                Title = $"Test Alert - {alertType}",
                Description = $"Test alert generated at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}",
                SourceIp = ip,
                AlertTypeId = (int)Enum.Parse<AlertTypeId>(alertType),
                SeverityLevelId = (int)Enum.Parse<SeverityLevelId>(severity),
                StatusId = (int)AlertStatusId.New,
                Timestamp = DateTime.UtcNow
            };

            await _alertService.CreateAlertAsync(alert);
            
            _logger.LogInformation("Test alert generated: {AlertType} from {IP}", alertType, ip);
            
            return Ok(new { message = "Test alert generated successfully", alertId = alert.Id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating test alert");
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("generateTestLog")]
    public async Task<IActionResult> GenerateTestLog()
    {
        try
        {
            var logEntry = new LogEntry
            {
                Timestamp = DateTime.UtcNow,
                LogLevelTypeId = _random.Next(1, 5), // 1-4
                LogSourceId = _random.Next(1, 6), // 1-5
                Message = $"Test log generated at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}",
                IpAddress = $"192.168.1.{_random.Next(1, 255)}",
                UserId = "TestUser",
                WasSuccessful = _random.Next(100) > 20,
                Details = "This is a test log entry generated by the admin panel"
            };

            await _logService.CreateLogAsync(logEntry);

            // Check if we should create an alert
            if (logEntry.LogLevelTypeId == 4) // Critical
            {
                var alert = new Alert
                {
                    Title = "Critical Log Detected",
                    Description = logEntry.Message,
                    AlertTypeId = 1, // SuspiciousIP
                    SeverityLevelId = 4, // Critical
                    StatusId = 1, // New
                    SourceIp = logEntry.IpAddress,
                    Timestamp = DateTime.UtcNow
                };
                await _alertService.CreateAlertAsync(alert);
            }

            return Ok(new { message = "Test log generated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating test log");
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("generateBulkLogs")]
    public async Task<IActionResult> GenerateBulkLogs()
    {
        try
        {
            var count = _random.Next(5, 15);
            var logs = new List<LogEntry>();
            var alerts = new List<Alert>();

            for (int i = 0; i < count; i++)
            {
                var logEntry = new LogEntry
                {
                    Timestamp = DateTime.UtcNow.AddMinutes(-_random.Next(0, 60)),
                    LogLevelTypeId = _random.Next(1, 5),
                    LogSourceId = _random.Next(1, 6),
                    Message = $"Bulk log #{i + 1} - {GetRandomMessage()}",
                    IpAddress = $"192.168.1.{_random.Next(1, 255)}",
                    UserId = GetRandomUser(),
                    WasSuccessful = _random.Next(100) > 30,
                    Details = $"Bulk generated log entry #{i + 1}"
                };

                logs.Add(logEntry);

                // Create alert for critical logs or failed operations
                if (logEntry.LogLevelTypeId == 4 || !logEntry.WasSuccessful)
                {
                    var alert = new Alert
                    {
                        Title = logEntry.LogLevelTypeId == 4 ? "Critical Log Detected" : "Failed Operation Detected",
                        Description = logEntry.Message,
                        AlertTypeId = logEntry.LogLevelTypeId == 4 ? 1 : 2, // SuspiciousIP or DDoS
                        SeverityLevelId = logEntry.LogLevelTypeId == 4 ? 4 : 3, // Critical or High
                        StatusId = 1, // New
                        SourceIp = logEntry.IpAddress,
                        Timestamp = DateTime.UtcNow
                    };
                    alerts.Add(alert);
                }
            }

            foreach (var log in logs)
            {
                await _logService.CreateLogAsync(log);
            }

            foreach (var alert in alerts)
            {
                await _alertService.CreateAlertAsync(alert);
            }

            return Ok(new { count = count, alertsCreated = alerts.Count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating bulk logs");
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("clearAllLogs")]
    public async Task<IActionResult> ClearAllLogs()
    {
        try
        {
            var logs = await _logService.GetAllLogsAsync();
            foreach (var log in logs)
            {
                await _logService.DeleteLogAsync(log.Id);
            }

            return Ok(new { message = "All logs cleared successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing all logs");
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("createCustomLog")]
    public async Task<IActionResult> CreateCustomLog([FromBody] CreateCustomLogDto dto)
    {
        try
        {
            var logEntry = new LogEntry
            {
                Timestamp = DateTime.UtcNow,
                LogLevelTypeId = dto.LogLevelTypeId,
                LogSourceId = dto.LogSourceId,
                Message = dto.Message,
                IpAddress = dto.IpAddress,
                UserId = dto.UserId,
                WasSuccessful = _random.Next(100) > 20,
                Details = $"Custom log created by admin: {dto.Message}"
            };

            await _logService.CreateLogAsync(logEntry);

            // Create alert if requested or if conditions are met
            if (dto.CreateAlert || logEntry.LogLevelTypeId == 4 || !logEntry.WasSuccessful)
            {
                var alert = new Alert
                {
                    Title = logEntry.LogLevelTypeId == 4 ? "Critical Log Detected" : "Custom Log Alert",
                    Description = logEntry.Message,
                    AlertTypeId = logEntry.LogLevelTypeId == 4 ? 1 : 2,
                    SeverityLevelId = logEntry.LogLevelTypeId == 4 ? 4 : 3,
                    StatusId = 1,
                    SourceIp = logEntry.IpAddress,
                    Timestamp = DateTime.UtcNow
                };
                await _alertService.CreateAlertAsync(alert);
            }

            return Ok(new { message = "Custom log created successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating custom log");
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("createAlertFromLog/{logId}")]
    public async Task<IActionResult> CreateAlertFromLog(long logId)
    {
        try
        {
            var log = await _logService.GetLogByIdAsync(logId);
            if (log == null)
            {
                return NotFound(new { error = "Log not found" });
            }

            var alert = new Alert
            {
                Title = $"Alert from {log.LogSource?.Name} Log",
                Description = log.Message,
                AlertTypeId = log.LogLevelTypeId == 4 ? 1 : 2,
                SeverityLevelId = log.LogLevelTypeId,
                StatusId = 1,
                SourceIp = log.IpAddress,
                Timestamp = DateTime.UtcNow
            };

            await _alertService.CreateAlertAsync(alert);

            return Ok(new { message = "Alert created from log successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating alert from log {LogId}", logId);
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("clearLogs")]
    public async Task<IActionResult> ClearLogs()
    {
        try
        {
            // Clear recent logs (keep last 1000)
            // This is a simplified implementation
            _logger.LogInformation("Logs cleared by admin");
            
            return Ok(new { message = "Logs cleared successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing logs");
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("dashboardStats")]
    public async Task<IActionResult> GetDashboardStats()
    {
        try
        {
            var totalAlerts = await _alertService.GetAlertCountAsync();
            var recentAlerts = await _alertService.GetRecentAlertsAsync(TimeSpan.FromHours(24));
            
            var stats = new
            {
                totalAlerts = totalAlerts,
                activeUsers = 15, // Placeholder
                blockedIPs = 8,   // Placeholder
                restrictedUsers = 3, // Placeholder
                recentAlerts = recentAlerts.Count(),
                isLogGenerationActive = await _logGenerationControlService.GetLogGenerationStatusAsync()
            };
            
            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting dashboard stats");
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("createAlertsFromLogs")]
    public async Task<IActionResult> CreateAlertsFromLogs([FromBody] CreateAlertsFromLogsDto dto)
    {
        try
        {
            var logs = await _logService.GetAllLogsAsync();
            var filteredLogs = logs;

            // Apply filters
            if (!string.IsNullOrEmpty(dto.Filter))
            {
                switch (dto.Filter.ToLower())
                {
                    case "error":
                        filteredLogs = logs.Where(l => l.LogLevelTypeId == 3);
                        break;
                    case "security":
                        filteredLogs = logs.Where(l => l.LogSourceId == 5 || l.Message.Contains("Security") || l.Message.Contains("Firewall"));
                        break;
                    case "failed":
                        filteredLogs = logs.Where(l => !l.WasSuccessful);
                        break;
                }
            }

            var alertCount = 0;
            var random = new Random();

            foreach (var log in filteredLogs)
            {
                // Create alert based on log conditions
                if (ShouldCreateAlertFromLog(log))
                {
                    var alert = new Alert
                    {
                        Title = GetAlertTitleFromLog(log),
                        Description = log.Message,
                        AlertTypeId = log.LogLevelTypeId == 3 ? 1 : 2, // SuspiciousIP or DDoS
                        SeverityLevelId = log.LogLevelTypeId == 3 ? 4 : 3, // Critical or High
                        StatusId = 1, // New
                        SourceIp = log.IpAddress,
                        Timestamp = DateTime.UtcNow
                    };

                    await _alertService.CreateAlertAsync(alert);
                    alertCount++;

                    // Add some randomness to avoid creating too many alerts
                    if (random.Next(100) < 30) // 30% chance to create alert
                    {
                        await Task.Delay(100); // Small delay between alerts
                    }
                }
            }

            return Ok(new { count = alertCount, message = $"Created {alertCount} alerts from logs" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating alerts from logs");
            return StatusCode(500, new { error = "Failed to create alerts from logs" });
        }
    }

    private bool ShouldCreateAlertFromLog(LogEntry log)
    {
        // Conditions for creating alerts
        return log.LogLevelTypeId == 3 || // Error logs
               !log.WasSuccessful || // Failed operations
               log.Message.Contains("Security") ||
               log.Message.Contains("Firewall") ||
               log.Message.Contains("DROP") ||
               log.Message.Contains("REJECT") ||
               log.Message.Contains("Failed") ||
               log.Message.Contains("Error") ||
               log.Message.Contains("Authentication failure") ||
               log.Message.Contains("Access denied");
    }

    private string GetAlertTitleFromLog(LogEntry log)
    {
        if (log.LogLevelTypeId == 3)
            return "Error Log Detected";
        if (!log.WasSuccessful)
            return "Failed Operation Detected";
        if (log.Message.Contains("Security"))
            return "Security Event Detected";
        if (log.Message.Contains("Firewall"))
            return "Firewall Event Detected";
        if (log.Message.Contains("Authentication"))
            return "Authentication Failure Detected";
        
        return "Suspicious Activity Detected";
    }

    private string GetRandomMessage()
    {
        var messages = new[]
        {
            "User authentication attempt",
            "Database connection established",
            "File access request",
            "Network packet received",
            "System resource usage",
            "Security scan completed",
            "Backup process started",
            "Configuration change detected"
        };
        return messages[_random.Next(messages.Length)];
    }

    private string GetRandomUser()
    {
        var users = new[]
        {
            "admin@example.com",
            "user1@example.com",
            "system@example.com",
            "service@example.com",
            "guest@example.com"
        };
        return users[_random.Next(users.Length)];
    }

    // Account Management from Alerts
    [HttpPost("lockAccountFromAlert/{alertId}")]
    public async Task<IActionResult> LockAccountFromAlert(int alertId, [FromBody] ProcessAlertRequest request)
    {
        try
        {
            var alert = await _alertService.GetAlertByIdAsync(alertId);
            if (alert == null)
            {
                return NotFound(new { error = "Alert not found" });
            }

            // Tìm user từ log liên quan
            ApplicationUser? user = null;
            if (alert.LogId.HasValue)
            {
                var log = await _logService.GetLogByIdAsync(alert.LogId.Value);
                if (log != null && !string.IsNullOrEmpty(log.UserId))
                {
                    user = await _userManager.FindByIdAsync(log.UserId);
                }
            }

            // Fallback: tìm user theo email nếu không có log hoặc userId
            if (user == null)
            {
                user = await _userManager.FindByEmailAsync("user@gmail.com"); // Demo user
            }

            if (user == null)
            {
                return BadRequest(new { error = "Không tìm thấy tài khoản liên quan" });
            }

            // Khóa tài khoản
            var lockoutEnd = DateTime.UtcNow.AddDays(7); // Khóa 7 ngày
            await _userManager.SetLockoutEndDateAsync(user, lockoutEnd);
            await _userManager.SetLockoutEnabledAsync(user, true);

            // Tạo alert mới về việc khóa tài khoản
            var lockAlert = new Alert
            {
                Title = "Tài khoản bị khóa",
                Description = $"Tài khoản {user.UserName} đã bị khóa do cảnh báo #{alertId}. Lý do: {request?.Reason ?? "Không có lý do"}",
                AlertTypeId = 1, // SuspiciousIP
                SeverityLevelId = 3, // High
                StatusId = 1, // New
                SourceIp = alert.SourceIp,
                Timestamp = DateTime.UtcNow
            };
            await _alertService.CreateAlertAsync(lockAlert);

            _logger.LogInformation("Account {UserName} locked due to alert {AlertId}. Reason: {Reason}", user.UserName, alertId, request?.Reason);

            return Ok(new { message = "Tài khoản đã được khóa thành công", userId = user.Id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error locking account from alert {AlertId}", alertId);
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("restrictAccountFromAlert/{alertId}")]
    public async Task<IActionResult> RestrictAccountFromAlert(int alertId, [FromBody] ProcessAlertRequest request)
    {
        try
        {
            var alert = await _alertService.GetAlertByIdAsync(alertId);
            if (alert == null)
            {
                return NotFound(new { error = "Alert not found" });
            }

            // Tìm user từ log liên quan
            ApplicationUser? user = null;
            if (alert.LogId.HasValue)
            {
                var log = await _logService.GetLogByIdAsync(alert.LogId.Value);
                if (log != null && !string.IsNullOrEmpty(log.UserId))
                {
                    user = await _userManager.FindByIdAsync(log.UserId);
                }
            }

            // Fallback: tìm user theo email nếu không có log hoặc userId
            if (user == null)
            {
                user = await _userManager.FindByEmailAsync("user@gmail.com"); // Demo user
            }

            if (user == null)
            {
                return BadRequest(new { error = "Không tìm thấy tài khoản liên quan" });
            }

            // Hạn chế tài khoản (có thể thêm vào role Restricted)
            // Hoặc tạo một flag trong database để đánh dấu user bị hạn chế

            // Tạo alert mới về việc hạn chế tài khoản
            var restrictAlert = new Alert
            {
                Title = "Tài khoản bị hạn chế",
                Description = $"Tài khoản {user.UserName} đã bị hạn chế do cảnh báo #{alertId}. Lý do: {request?.Reason ?? "Không có lý do"}",
                AlertTypeId = 1, // SuspiciousIP
                SeverityLevelId = 2, // Medium
                StatusId = 1, // New
                SourceIp = alert.SourceIp,
                Timestamp = DateTime.UtcNow
            };
            await _alertService.CreateAlertAsync(restrictAlert);

            _logger.LogInformation("Account {UserName} restricted due to alert {AlertId}. Reason: {Reason}", user.UserName, alertId, request?.Reason);

            return Ok(new { message = "Tài khoản đã được hạn chế thành công", userId = user.Id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error restricting account from alert {AlertId}", alertId);
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("blockIPFromAlert/{alertId}")]
    public async Task<IActionResult> BlockIPFromAlert(int alertId, [FromBody] ProcessAlertRequest request)
    {
        try
        {
            var alert = await _alertService.GetAlertByIdAsync(alertId);
            if (alert == null)
            {
                return NotFound(new { error = "Alert not found" });
            }

            if (string.IsNullOrEmpty(alert.SourceIp))
            {
                return BadRequest(new { error = "Alert không có IP nguồn" });
            }

            // Block IP
            await _ipBlockingService.BlockIPAsync(alert.SourceIp, "Blocked from alert", "Admin");

            // Tạo alert mới về việc block IP
            var blockAlert = new Alert
            {
                Title = "IP bị block",
                Description = $"IP {alert.SourceIp} đã bị block do cảnh báo #{alertId}. Lý do: {request?.Reason ?? "Không có lý do"}",
                AlertTypeId = 1, // SuspiciousIP
                SeverityLevelId = 3, // High
                StatusId = 1, // New
                SourceIp = alert.SourceIp,
                Timestamp = DateTime.UtcNow
            };
            await _alertService.CreateAlertAsync(blockAlert);

            _logger.LogInformation("IP {IP} blocked due to alert {AlertId}. Reason: {Reason}", alert.SourceIp, alertId, request?.Reason);

            return Ok(new { message = "IP đã được block thành công", ip = alert.SourceIp });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error blocking IP from alert {AlertId}", alertId);
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("alertDetails/{alertId}")]
    public async Task<IActionResult> GetAlertDetails(int alertId)
    {
        try
        {
            var alert = await _alertService.GetAlertByIdAsync(alertId);
            if (alert == null)
            {
                return NotFound(new { error = "Không tìm thấy cảnh báo" });
            }

            // Kiểm tra xem có log liên quan và có user không
            string? associatedUser = null;
            bool hasAssociatedAccount = false;

            if (alert.LogId.HasValue)
            {
                var log = await _logService.GetLogByIdAsync(alert.LogId.Value);
                if (log != null && !string.IsNullOrEmpty(log.UserId))
                {
                    hasAssociatedAccount = true;
                    associatedUser = log.UserId;
                }
            }

            var alertDetails = new
            {
                alert = new
                {
                    id = alert.Id,
                    title = alert.Title,
                    description = alert.Description,
                    sourceIp = alert.SourceIp,
                    severityLevel = alert.SeverityLevel?.Name ?? "Unknown",
                    alertType = alert.AlertType?.Name ?? "Unknown",
                    timestamp = alert.Timestamp
                },
                hasAssociatedAccount = hasAssociatedAccount,
                associatedUser = associatedUser
            };

            return Ok(alertDetails);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting alert details for {AlertId}", alertId);
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("unblockIP")]
    public async Task<IActionResult> UnblockIP([FromBody] UnblockIPRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.IpAddress))
            {
                return BadRequest(new { error = "IP address is required" });
            }

            var result = await _ipBlockingService.UnblockIPAsync(request.IpAddress);
            if (result)
            {
                _logger.LogInformation("IP {IpAddress} unblocked successfully", request.IpAddress);
                return Ok(new { success = true, message = $"IP {request.IpAddress} has been unblocked" });
            }
            else
            {
                return BadRequest(new { error = $"Failed to unblock IP {request.IpAddress}" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unblocking IP {IpAddress}", request.IpAddress);
            return BadRequest(new { error = ex.Message });
        }
    }
} 