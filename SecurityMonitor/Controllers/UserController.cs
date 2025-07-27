using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecurityMonitor.Data;
using SecurityMonitor.DTOs.Logs;
using SecurityMonitor.DTOs.Common;
using SecurityMonitor.Models;
using SecurityMonitor.Services.Interfaces;
using System.Security.Claims;

namespace SecurityMonitor.Controllers
{
    [Authorize(Roles = "User")]
    public class UserController : Controller
    {
        private readonly IAlertService _alertService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public UserController(IAlertService alertService, UserManager<ApplicationUser> userManager, ApplicationDbContext context)
        {
            _alertService = alertService;
            _userManager = userManager;
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return RedirectToAction("Login", "Account", new { area = "Identity" });
            }
            
            // Tổng số cảnh báo liên quan đến user
            var totalAlerts = await _context.Alerts
                .Where(a => a.AssignedToId == userId)
                .CountAsync();
            
            // Số cảnh báo High và Critical chưa xử lý
            var importantAlerts = await _context.Alerts
                .Include(a => a.SeverityLevel)
                .Include(a => a.Status)
                .Where(a => a.AssignedToId == userId && 
                       (a.SeverityLevelId == (int)SeverityLevelId.High || a.SeverityLevelId == (int)SeverityLevelId.Critical) &&
                       a.StatusId == (int)AlertStatusId.New)
                .CountAsync();
            
            // Số lần đăng nhập trong 7 ngày qua
            var recentLogins = await _context.AuditLogs
                .Where(l => l.UserId == userId && 
                       !l.Action.Contains("Failed") &&  // Chỉ đếm các lần đăng nhập thành công
                       l.Action.Contains("Login") &&
                       l.Timestamp >= DateTime.UtcNow.AddDays(-7))
                .CountAsync();
            
            // Lịch sử đăng nhập gần đây
            var loginHistory = await _context.AuditLogs
                .Include(l => l.User)
                .Where(l => l.UserId == userId && 
                       (l.Action.Contains("Login") || l.Action.Contains("Password")))
                .OrderByDescending(l => l.Timestamp)
                .Take(5)
                .Select(l => new AuditLogDto(
                    l.Id,
                    l.Timestamp,
                    l.User != null ? l.User.Email : "",
                    l.Action,
                    "Authentication",
                    l.EntityId,
                    l.Details,
                    l.IpAddress
                ))
                .ToListAsync();

            // Cảnh báo gần đây nhất
            var recentAlerts = await _context.Alerts
                .Include(a => a.SeverityLevel)
                .Include(a => a.Status)
                .Where(a => a.AssignedToId == userId)
                .OrderByDescending(a => a.Timestamp)
                .Take(5)
                .Select(a => new AlertSummaryDto(
                    a.Timestamp,
                    a.SourceIp,
                    a.Description,
                    a.SeverityLevel.Name,
                    a.Status.Name
                ))
                .ToListAsync();

            var dashboardData = new SecurityMonitor.DTOs.Dashboard.UserDashboardDto(
                TotalAlerts: totalAlerts,
                ImportantAlerts: importantAlerts,
                RecentLogins: recentLogins,
                RecentLoginHistory: loginHistory,
                RecentAlerts: recentAlerts
            );

            return View(dashboardData);
        }

        public async Task<IActionResult> Alerts()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return RedirectToAction("Login", "Account", new { area = "Identity" });
            }

            var alerts = await _alertService.GetUserAlertsAsync(userId);
            return View(alerts);
        }

        public async Task<IActionResult> Profile()
        {
            var user = await _userManager.GetUserAsync(User);
            return View(user);
        }

        [HttpPost]
        public async Task<IActionResult> ChangePassword(string currentPassword, string newPassword)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login", "Account", new { area = "Identity" });
            }

            var result = await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);
            
            if (result.Succeeded)
            {
                TempData["SuccessMessage"] = "Mật khẩu đã được thay đổi thành công.";
                return RedirectToAction(nameof(Profile));
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error.Description);
            }

            return View("Profile");
        }

        public async Task<IActionResult> MyLogs(string? filter, int page = 1)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var pageSize = 10;

            // Lấy logs của user hiện tại 
            var query = _context.AuditLogs
                .Where(l => l.UserId == userId);

            // Áp dụng filter
            if (!string.IsNullOrEmpty(filter))
            {
                switch (filter.ToLower())
                {
                    case "success":
                        query = query.Where(l => l.Action == "Login" || l.Action == "PasswordChange");
                        break;
                    case "failed":
                        query = query.Where(l => l.Action == "LoginFailed" || l.Action == "PasswordChangeFailed");
                        break;
                }
            }

            // Đếm tổng số items và tính số trang
            var totalItems = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
            page = Math.Min(Math.Max(1, page), totalPages);

            // Lấy dữ liệu theo trang và map sang DTO
            var logs = await query
                .OrderByDescending(l => l.Timestamp)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(l => new MyLogDto(
                    l.Timestamp,
                    l.IpAddress,
                    l.EntityType ?? "Unknown",
                    l.Action ?? "Unknown",
                    !string.IsNullOrEmpty(l.Action) && !l.Action.Contains("Failed"),
                    l.Details
                ))
                .ToListAsync();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.Filter = filter;

            return View(logs);
        }
    }
}