using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecurityMonitor.Data;
using SecurityMonitor.DTOs;
using SecurityMonitor.DTOs.Dashboard;
using SecurityMonitor.Models;
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

        // GET: Admin/Users
        public async Task<IActionResult> Users()
        {
            var users = await _userManager.Users
                .Select(u => new UserDto
                {
                    Id = u.Id,
                    Email = u.Email,
                    FullName = u.FullName ?? u.UserName, // Use FullName if available, otherwise UserName
                    IsEmailConfirmed = u.EmailConfirmed,
                    LastLoginTime = u.LastLoginTime,
                    LoginCount = 0 // Since we don't track login count, defaulting to 0
                })
                .ToListAsync();

            return View(users);
        }
    }
}
