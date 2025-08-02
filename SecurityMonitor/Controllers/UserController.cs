using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecurityMonitor.Services.Interfaces;
using SecurityMonitor.Data;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using SecurityMonitor.DTOs.Dashboard;
using SecurityMonitor.DTOs.Logs;
using SecurityMonitor.DTOs.Common;

namespace SecurityMonitor.Controllers
{
    [Authorize]
    public class UserController : Controller
    {
        private readonly IAlertService _alertService;
        private readonly ILogService _logService;
        private readonly ILogAnalysisService _logAnalysisService;
        private readonly ApplicationDbContext _context;

        public UserController(
            IAlertService alertService,
            ILogService logService,
            ILogAnalysisService logAnalysisService,
            ApplicationDbContext context)
        {
            _alertService = alertService;
            _logService = logService;
            _logAnalysisService = logAnalysisService;
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userName = User.Identity?.Name;

            // Lấy thống kê cho user hiện tại
            var userStats = await GetUserStats(userId, userName);

            return View(userStats);
        }

        [HttpGet("user/logs")]
        public async Task<IActionResult> GetUserLogs()
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var userName = User.Identity?.Name;

                // Lấy logs của user hiện tại
                var userLogs = await _context.LogEntries
                    .Where(l => l.UserId == userName)
                    .OrderByDescending(l => l.Timestamp)
                    .Take(50)
                    .Select(l => new
                    {
                        id = l.Id,
                        timestamp = l.Timestamp,
                        message = l.Message,
                        level = l.LogLevelType.Name,
                        ipAddress = l.IpAddress,
                        wasSuccessful = l.WasSuccessful,
                        analysis = l.LogAnalyses.Select(la => new
                        {
                            analysisResult = la.AnalysisResult,
                            riskLevel = la.RiskLevel,
                            isAnomaly = la.IsAnomaly,
                            isThreat = la.IsThreat
                        }).FirstOrDefault(),
                        alert = l.Alerts.Select(a => new
                        {
                            id = a.Id,
                            title = a.Title,
                            severity = a.SeverityLevelId
                        }).FirstOrDefault()
                    })
                    .ToListAsync();

                return Json(new { success = true, logs = userLogs });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        [HttpGet("user/stats")]
        public async Task<IActionResult> GetUserStats()
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var userName = User.Identity?.Name;

                var stats = await GetUserStats(userId, userName);

                return Json(new { success = true, stats = stats });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        private async Task<UserDashboardDto> GetUserStats(string userId, string userName)
        {
            // Đếm logs của user
            var userLogsCount = await _context.LogEntries
                .Where(l => l.UserId == userName)
                .CountAsync();

            // Đếm alerts được tạo từ logs của user
            var userAlertsCount = await _context.Alerts
                .Where(a => a.SourceIp != null && a.LogId != null)
                .Join(_context.LogEntries, a => a.LogId, l => l.Id, (a, l) => new { Alert = a, Log = l })
                .Where(x => x.Log.UserId == userName)
                .CountAsync();

            // Đếm anomalies
            var userAnomaliesCount = await _context.LogAnalyses
                .Where(la => la.LogEntry.UserId == userName && la.IsAnomaly)
                .CountAsync();

            // Đếm threats
            var userThreatsCount = await _context.LogAnalyses
                .Where(la => la.LogEntry.UserId == userName && la.IsThreat)
                .CountAsync();

            // Lấy recent login history
            var recentLoginHistory = await _context.AuditLogs
                .Include(al => al.User)
                .Where(al => al.UserId == userId && al.Action == "Login")
                .OrderByDescending(al => al.Timestamp)
                .Take(10)
                .Select(al => new
                {
                    al.Id,
                    al.Timestamp,
                    al.Action,
                    al.EntityType,
                    al.EntityId,
                    al.Details,
                    al.IpAddress,
                    UserEmail = al.User != null ? al.User.UserName : al.UserId // Hiển thị username thay vì email
                })
                .ToListAsync();

            var auditLogDtos = recentLoginHistory.Select(al => new AuditLogDto(
                Id: al.Id,
                Timestamp: al.Timestamp,
                UserEmail: al.UserEmail,
                Action: al.Action,
                EntityType: al.EntityType,
                EntityId: al.EntityId,
                Details: al.Details,
                IpAddress: al.IpAddress
            )).ToList();

            // Lấy recent alerts
            var recentAlerts = await _context.Alerts
                .Where(a => a.SourceIp != null && a.LogId != null)
                .Join(_context.LogEntries, a => a.LogId, l => l.Id, (a, l) => new { Alert = a, Log = l })
                .Where(x => x.Log.UserId == userName)
                .OrderByDescending(x => x.Alert.Timestamp)
                .Take(5)
                .Select(x => new
                {
                    x.Alert.Timestamp,
                    x.Alert.SourceIp,
                    x.Alert.Description,
                    SeverityLevel = x.Alert.SeverityLevel != null ? x.Alert.SeverityLevel.Name : "Unknown",
                    Status = x.Alert.Status != null ? x.Alert.Status.Name : "Unknown"
                })
                .ToListAsync();

            var alertSummaryDtos = recentAlerts.Select(x => new AlertSummaryDto(
                Timestamp: x.Timestamp,
                SourceIp: x.SourceIp,
                Description: x.Description,
                SeverityLevel: x.SeverityLevel,
                Status: x.Status
            )).ToList();

            return new UserDashboardDto(
                TotalAlerts: userAlertsCount,
                ImportantAlerts: userThreatsCount,
                RecentLogins: userLogsCount,
                RecentLoginHistory: auditLogDtos,
                RecentAlerts: alertSummaryDtos
            );
        }
    }
}