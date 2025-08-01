using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SecurityMonitor.Data;
using SecurityMonitor.DTOs;
using SecurityMonitor.DTOs.Dashboard;
using SecurityMonitor.DTOs.Security;
using SecurityMonitor.DTOs.Logs;
using SecurityMonitor.Hubs;
using SecurityMonitor.Models;
using SecurityMetricsDto = SecurityMonitor.DTOs.Security.SecurityMetricsDto;
using SystemStatsDto = SecurityMonitor.DTOs.Security.SystemStatsDto;
using SecurityMonitor.Services;
using SecurityMonitor.Services.Interfaces;
using SecurityMonitor.Extensions;
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
        private readonly IAlertService _alertService;
        private readonly ILogService _logService;
        private readonly ILogger<AdminController> _logger;

        public AdminController(
            ApplicationDbContext context, 
            IIPCheckerService ipChecker,
            UserManager<ApplicationUser> userManager,
            IAlertService alertService,
            ILogService logService,
            ILogger<AdminController> logger)
        {
            _context = context;
            _ipChecker = ipChecker;
            _userManager = userManager;
            _alertService = alertService;
            _logService = logService;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            var totalAlerts = await _alertService.GetAlertCountAsync();
            var recentAlerts = await _alertService.GetRecentAlertsAsync(TimeSpan.FromHours(24));
            
            // Lấy trạng thái log generation từ service
            var logControlService = HttpContext.RequestServices.GetRequiredService<ILogGenerationControlService>();
            var isLogGenerationActive = await logControlService.GetLogGenerationStatusAsync();
            
            var dashboardDto = new AdminDashboardDto(
                TotalAlerts: totalAlerts,
                ActiveUsers: 15, // Placeholder
                BlockedIPs: 8,   // Placeholder
                RestrictedUsers: 3, // Placeholder
                RecentAlerts: recentAlerts.Count(),
                IsLogGenerationActive: isLogGenerationActive,
                RecentAlertsList: new List<AlertDto>(),
                RecentActivity: new List<ActivityDto>()
            );

            // Log access to admin dashboard
            var currentUser = await _userManager.GetUserAsync(User);
            var clientIp = Request.GetClientIpAddress();
            var logEntry = new LogEntry
            {
                Message = $"Admin {currentUser?.UserName} accessed Admin Dashboard",
                LogSourceId = 1, // Custom App
                LogLevelTypeId = 1, // Information
                IpAddress = clientIp,
                UserId = currentUser?.Id,
                WasSuccessful = true,
                Details = $"Admin dashboard access from IP {clientIp}"
            };
            
            await _logService.CreateLogAsync(logEntry);

            return View(dashboardDto);
        }

        public async Task<IActionResult> Alerts()
        {
            var alerts = await _alertService.GetAllAlertsAsync();
            return View(alerts);
        }

        public async Task<IActionResult> Logs(int page = 1, int pageSize = 20)
        {
            var allLogs = await _logService.GetAllLogsAsync();
            var totalCount = allLogs.Count();
            var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);
            
            var logs = allLogs
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalCount = totalCount;

            return View(logs);
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

        public IActionResult SematextIntegration()
        {
            return View();
        }

        public IActionResult LogAnalysis()
        {
            return View();
        }

        // Multi-Source Logs
        public async Task<IActionResult> MultiSourceLogs()
        {
            var logs = await _context.LogEntries
                .Include(l => l.LogSource)
                .Include(l => l.LogLevelType)
                .OrderByDescending(l => l.Timestamp)
                .Take(100)
                .Select(l => new MyLogDto(
                    l.Timestamp,
                    l.IpAddress,
                    l.LogSource.Name,
                    l.LogLevelType != null ? l.LogLevelType.Name : "Information",
                    l.WasSuccessful,
                    l.Message,
                    l.UserId ?? "System",
                    l.Details
                ))
                .ToListAsync();

            return View(logs);
        }
    }
}
