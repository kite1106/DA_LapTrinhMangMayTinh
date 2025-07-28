using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SecurityMonitor.Data;
using SecurityMonitor.DTOs;
using SecurityMonitor.DTOs.Dashboard;
using SecurityMonitor.DTOs.Security;
using SecurityMonitor.Hubs;
using SecurityMonitor.Models;
using SecurityMetricsDto = SecurityMonitor.DTOs.Security.SecurityMetricsDto;
using SystemStatsDto = SecurityMonitor.DTOs.Security.SystemStatsDto;
using SecurityMonitor.Services;
using SecurityMonitor.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SecurityMonitor.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IIPCheckerService _ipChecker;
        private readonly UserManager<ApplicationUser> _userManager;

        public AdminController(
            ApplicationDbContext context, 
            IIPCheckerService ipChecker,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _ipChecker = ipChecker;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var model = new DashboardDataDto
            {
                TotalUsersCount = await _userManager.Users.CountAsync(),
                TotalAlertsCount = await _context.Alerts.CountAsync(),
                BlockedIPsCount = await _context.BlockedIPs.CountAsync(),
                TotalLogsCount = await _context.Logs.CountAsync(),
                RecentActivities = await _context.AuditLogs
                    .Where(x => x.Action != "Create" || (x.Details != null && !x.Details.StartsWith("Created log from source")))  // Lọc bỏ các log ảo
                    .Where(x => !string.IsNullOrEmpty(x.IpAddress) 
                           && x.IpAddress != "127.0.0.1" 
                           && x.IpAddress != "::1"
                           && x.IpAddress != "localhost")  // Lọc bỏ localhost
                    .OrderByDescending(x => x.Timestamp)
                    .Take(10)
                    .Select(x => new RecentActivityDto
                    {
                        Timestamp = x.Timestamp,
                        IpAddress = x.IpAddress,
                        UserId = x.UserId,
                        Action = x.Action,
                        Details = x.Details ?? string.Empty
                    })
                    .ToListAsync()
            };

            // Dữ liệu cho biểu đồ cảnh báo theo thời gian (7 ngày gần nhất)
            var last7Days = Enumerable.Range(0, 7)
                .Select(i => DateTime.Today.AddDays(-i))
                .Reverse()
                .ToList();

            var alertsByDay = await _context.Alerts
                .Where(a => a.Timestamp >= DateTime.Today.AddDays(-7))
                .GroupBy(a => a.Timestamp.Date)
                .Select(g => new { Date = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Date, x => x.Count);

            model.AlertsChartData.Labels = last7Days.Select(d => d.ToString("dd/MM")).ToList();
            model.AlertsChartData.Data = last7Days
                .Select(d => alertsByDay.GetValueOrDefault(d.Date, 0))
                .ToList();

            // Dữ liệu cho biểu đồ phân bố loại cảnh báo
            var alertTypes = await _context.Alerts
                .GroupBy(a => a.AlertType.Name)
                .Select(g => new { Type = g.Key, Count = g.Count() })
                .ToListAsync();

            model.AlertTypesChartData.Labels = alertTypes.Select(t => t.Type).ToList();
            model.AlertTypesChartData.Data = alertTypes.Select(t => t.Count).ToList();

            // Thêm dữ liệu cho Security Metrics
            var now = DateTime.UtcNow;
            var last24Hours = now.AddHours(-24);

            // Endpoint nhạy cảm
            var sensitiveEndpoints = await _context.AuditLogs
                .Where(l => l.Timestamp >= last24Hours && 
                           ((l.Path ?? "").StartsWith("/admin") || (l.Path ?? "").StartsWith("/config") || (l.Path ?? "").Contains("api")))
                .ToListAsync();

            model.SecurityMetrics.SensitiveEndpoints = new()
            {
                TotalAccessAttempts = sensitiveEndpoints.Count,
                UnauthorizedAttempts = sensitiveEndpoints.Count(x => x.StatusCode == 401 || x.StatusCode == 403),
                BlockedAttempts = sensitiveEndpoints.Count(x => x.StatusCode == 403),
                RecentAccesses = sensitiveEndpoints
                    .OrderByDescending(x => x.Timestamp)
                    .Take(5)
                    .Select(x => new EndpointAccess
                    {
                        Endpoint = x.Path ?? "unknown",
                        IpAddress = x.IpAddress ?? "unknown",
                        Timestamp = x.Timestamp,
                        StatusCode = x.StatusCode
                    })
                    .ToList()
            };

            // Hành vi bất thường
            var suspiciousIPs = await _context.AuditLogs
                .Where(l => l.Timestamp >= last24Hours)
                .GroupBy(l => l.IpAddress)
                .Select(g => new
                {
                    IP = g.Key,
                    RequestCount = g.Count(),
                    ErrorCount = g.Count(x => x.StatusCode >= 400)
                })
                .ToListAsync();

            model.SecurityMetrics.Anomalies = new()
            {
                HighRequestRateIPs = suspiciousIPs.Count(x => x.RequestCount > 100),
                ScanningAttempts = suspiciousIPs.Count(x => x.ErrorCount > 20),
                PotentialDDoSAlerts = suspiciousIPs.Count(x => x.RequestCount > 1000),
                SuspiciousIPs = suspiciousIPs
                    .Where(x => x.RequestCount > 100 || x.ErrorCount > 20)
                    .Select(x => new IPActivity
                    {
                        IpAddress = x.IP ?? "unknown",
                        RequestsPerMinute = x.RequestCount / 1440, // per minute in 24h
                        ErrorCount = x.ErrorCount,
                        ActivityType = x.RequestCount > 1000 ? "DDoS" : x.ErrorCount > 20 ? "Scanner" : "High Traffic"
                    })
                    .ToList()
            };

            // System Errors
            var systemErrors = await _context.AuditLogs
                .Where(l => l.Timestamp >= last24Hours && l.StatusCode >= 500)
                .ToListAsync();

            model.SecurityMetrics.SystemErrors = new()
            {
                TotalErrors = systemErrors.Count,
                ConsecutiveErrors = GetMaxConsecutiveErrors(systemErrors),
                UniqueErrorTypes = systemErrors.Select(x => x.Details).Distinct().Count(),
                RecentErrors = systemErrors
                    .GroupBy(x => x.Details)
                    .Select(g => new ErrorEvent
                    {
                        ErrorType = g.Key ?? "Unknown",
                        Timestamp = g.Max(x => x.Timestamp),
                        Source = g.First().Path ?? "unknown",
                        Count = g.Count()
                    })
                    .OrderByDescending(x => x.Count)
                    .Take(5)
                    .ToList()
            };

            // User Behavior
            var userActivities = await _context.AuditLogs
                .Where(l => l.Timestamp >= last24Hours && 
                           (l.Action.Contains("Password") || l.Action.Contains("Email") || l.Action == "Login"))
                .ToListAsync();

            model.SecurityMetrics.Behaviors = new()
            {
                PasswordResetAttempts = userActivities.Count(x => x.Action.Contains("Password")),
                EmailChangeAttempts = userActivities.Count(x => x.Action.Contains("Email")),
                SuspiciousActivities = userActivities.Count(x => x.StatusCode >= 400),
                RecentActivities = userActivities
                    .OrderByDescending(x => x.Timestamp)
                    .Take(5)
                    .Select(x => new UserActivity
                    {
                        UserId = x.UserId ?? "anonymous",
                        ActivityType = x.Action,
                        Timestamp = x.Timestamp,
                        Details = x.Details ?? "No details"
                    })
                    .ToList()
            };

            // Security Keywords
            var keywords = new[] { "injection", "xss", "csrf", "script", "select", "union", "delete", "drop" };
            var suspiciousRequests = await _context.AuditLogs
                .Where(l => l.Timestamp >= last24Hours && 
                           keywords.Any(k => (l.Path + " " + l.Details).ToLower().Contains(k)))
                .ToListAsync();

            model.SecurityMetrics.Keywords = new()
            {
                TotalDetections = suspiciousRequests.Count,
                HighRiskDetections = suspiciousRequests.Count(x => x.StatusCode >= 400),
                RecentDetections = suspiciousRequests
                    .OrderByDescending(x => x.Timestamp)
                    .Take(5)
                    .Select(x => new KeywordDetection
                    {
                        Keyword = keywords.First(k => (x.Path + " " + x.Details).ToLower().Contains(k)),
                        Context = x.Path ?? "unknown",
                        Timestamp = x.Timestamp,
                        Source = x.IpAddress ?? "unknown"
                    })
                    .ToList()
            };

            return View(model);
        }

        // GET: Admin/BlockedIPs
        public async Task<IActionResult> BlockedIPs()
        {
            try
            {
                var blockedIPs = await _context.BlockedIPs
                    .OrderByDescending(b => b.BlockedAt)
                    .ToListAsync();

                return View(blockedIPs);
            }
            catch (Exception ex)
            {
                // Log lỗi nếu có hệ thống log
                return Content($"Lỗi: {ex.Message}");
            }
        }

        // POST: Admin/BlockIP
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BlockIP(string ip, string reason)
        {
            if (string.IsNullOrWhiteSpace(ip) || string.IsNullOrWhiteSpace(reason))
            {
                TempData["ErrorMessage"] = "IP và lý do không được để trống.";
                return RedirectToAction(nameof(BlockedIPs));
            }

            // Kiểm tra IP hợp lệ (bạn có thể làm thêm regex hoặc validate chuyên sâu hơn)
            if (!System.Net.IPAddress.TryParse(ip, out _))
            {
                TempData["ErrorMessage"] = "Địa chỉ IP không hợp lệ.";
                return RedirectToAction(nameof(BlockedIPs));
            }

            // Kiểm tra IP đã tồn tại chưa
            var exists = await _context.BlockedIPs.AnyAsync(b => b.IpAddress == ip);
            if (exists)
            {
                TempData["ErrorMessage"] = "IP này đã bị chặn.";
                return RedirectToAction(nameof(BlockedIPs));
            }

            var blockedIP = new BlockedIP(
                ip,
                reason,
                User.Identity?.Name ?? "Admin");

            _context.BlockedIPs.Add(blockedIP);
            await _context.SaveChangesAsync();

            await _ipChecker.BlockIPAsync(ip);

            TempData["SuccessMessage"] = $"IP {ip} đã được chặn thành công.";

            return RedirectToAction(nameof(BlockedIPs));
        }

        // POST: Admin/UnblockIP
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UnblockIP(string ip)
        {
            if (string.IsNullOrWhiteSpace(ip))
            {
                TempData["ErrorMessage"] = "IP không hợp lệ.";
                return RedirectToAction(nameof(BlockedIPs));
            }

            var blockedIP = await _context.BlockedIPs.FirstOrDefaultAsync(b => b.IpAddress == ip);
            if (blockedIP != null)
            {
                _context.BlockedIPs.Remove(blockedIP);
                await _context.SaveChangesAsync();
                await _ipChecker.UnblockIPAsync(ip);
                TempData["SuccessMessage"] = $"IP {ip} đã được bỏ chặn.";
            }
            else
            {
                TempData["ErrorMessage"] = "Không tìm thấy IP để bỏ chặn.";
            }

            return RedirectToAction(nameof(BlockedIPs));
        }

        // Removed duplicate Users action - now handled by AccountManagementController

        // Removed duplicate LockUser and UnlockUser methods - now handled by AccountManagementController

        private int GetMaxConsecutiveErrors(List<AuditLog> errors)
        {
            if (!errors.Any()) return 0;

            var maxConsecutive = 1;
            var currentConsecutive = 1;
            var orderedErrors = errors.OrderBy(x => x.Timestamp).ToList();

            for (int i = 1; i < orderedErrors.Count; i++)
            {
                var timeDiff = (orderedErrors[i].Timestamp - orderedErrors[i - 1].Timestamp).TotalMinutes;
                
                if (timeDiff <= 5) // Consider errors within 5 minutes as consecutive
                {
                    currentConsecutive++;
                    maxConsecutive = Math.Max(maxConsecutive, currentConsecutive);
                }
                else
                {
                    currentConsecutive = 1;
                }
            }

            return maxConsecutive;
        }

        private async Task<Dictionary<string, int>> GetEndpointAccessMetrics(TimeSpan window)
        {
            var cutoffTime = DateTime.UtcNow.Subtract(window);
            var accessLogs = await _context.AuditLogs
                .Where(log => log.Timestamp >= cutoffTime)
                .GroupBy(log => log.Endpoint ?? "unknown")
                .Select(g => new { Endpoint = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Endpoint, x => x.Count);

            return accessLogs;
        }

        private async Task<List<UserBehaviorMetric>> GetUserBehaviorMetrics(TimeSpan window)
        {
            var cutoffTime = DateTime.UtcNow.Subtract(window);
            var behaviors = await _context.AuditLogs
                .Where(log => log.Timestamp >= cutoffTime && !string.IsNullOrEmpty(log.UserId))
                .GroupBy(log => log.UserId)
                .Select(g => new UserBehaviorMetric
                {
                    UserId = g.Key ?? "unknown",
                    RequestCount = g.Count(),
                    UniqueEndpoints = g.Select(l => l.Endpoint ?? "unknown").Distinct().Count(),
                    LastActivity = g.Max(l => l.Timestamp),
                    ErrorCount = g.Count(l => l.Level == "Error")
                })
                .ToListAsync();

            return behaviors;
        }

        private async Task<List<SecurityAlert>> GetSecurityAlerts(TimeSpan window)
        {
            var cutoffTime = DateTime.UtcNow.Subtract(window);
            var alerts = await _context.AuditLogs
                .Where(log => log.Timestamp >= cutoffTime && 
                       (log.Level == "Warning" || log.Level == "Error"))
                .OrderByDescending(log => log.Timestamp)
                .Select(log => new SecurityAlert
                {
                    Timestamp = log.Timestamp,
                    Level = log.Level ?? "Unknown",
                    Message = log.Message ?? "No message",
                    Source = log.Endpoint ?? "unknown",
                    UserId = log.UserId ?? "anonymous"
                })
                .ToListAsync();

            return alerts;
        }

        private async Task<IEnumerable<AnomalyDetection>> DetectAnomalies(TimeSpan window)
        {
            var anomalies = new List<AnomalyDetection>();
            var cutoffTime = DateTime.UtcNow.Subtract(window);
            
            // Get baseline metrics
            var baselineErrors = await _context.AuditLogs
                .Where(log => log.Timestamp >= cutoffTime && log.Level == "Error")
                .CountAsync();

            var baselineRequests = await _context.AuditLogs
                .Where(log => log.Timestamp >= cutoffTime)
                .CountAsync();

            // Error rate anomaly detection
            if (baselineRequests > 0)
            {
                var errorRate = (double)baselineErrors / baselineRequests;
                if (errorRate > 0.1) // More than 10% error rate
                {
                    anomalies.Add(new AnomalyDetection
                    {
                        Type = "HighErrorRate",
                        Severity = "High",
                        Description = $"High error rate detected: {errorRate:P2}",
                        Timestamp = DateTime.UtcNow
                    });
                }
            }

            // Rapid increase in requests
            var recentRequests = await _context.AuditLogs
                .Where(log => log.Timestamp >= DateTime.UtcNow.AddMinutes(-5))
                .CountAsync();

            var averageRequestsPer5Min = baselineRequests / (window.TotalMinutes / 5);
            if (recentRequests > averageRequestsPer5Min * 2)
            {
                anomalies.Add(new AnomalyDetection
                {
                    Type = "SuddenTrafficIncrease",
                    Severity = "Medium",
                    Description = $"Sudden increase in traffic detected. Current: {recentRequests}, Average: {averageRequestsPer5Min:F2}",
                    Timestamp = DateTime.UtcNow
                });
            }

            return anomalies;
        }

        [HttpGet]
        public async Task<IActionResult> SecurityMetrics()
        {
            var window = TimeSpan.FromHours(24); // 24-hour monitoring window
            
            var metrics = new SecurityMetricsDto
            {
                TimeWindow = "24 hours",
                EndpointMetrics = await GetEndpointAccessMetrics(window),
                UserBehaviorMetrics = await GetUserBehaviorMetrics(window),
                SecurityAlerts = await GetSecurityAlerts(window),
                Anomalies = await DetectAnomalies(window),
                SystemStats = new SystemStatsDto
                {
                    TotalRequests = await _context.AuditLogs
                        .Where(log => log.Timestamp >= DateTime.UtcNow.Subtract(window))
                        .CountAsync(),
                    ErrorCount = await _context.AuditLogs
                        .Where(log => log.Timestamp >= DateTime.UtcNow.Subtract(window) && 
                               log.Level == "Error")
                        .CountAsync(),
                    UniqueUsers = await _context.AuditLogs
                        .Where(log => log.Timestamp >= DateTime.UtcNow.Subtract(window))
                        .Select(log => log.UserId)
                        .Distinct()
                        .CountAsync(),
                    LastUpdateTime = DateTime.UtcNow
                }
            };

            // Detect consecutive errors
            var recentErrors = await _context.AuditLogs
                .Where(log => log.Timestamp >= DateTime.UtcNow.Subtract(window) && 
                       log.Level == "Error")
                .OrderBy(log => log.Timestamp)
                .ToListAsync();

            metrics.SystemStats.MaxConsecutiveErrors = GetMaxConsecutiveErrors(recentErrors);

            return View(metrics);
        }
    }
}
