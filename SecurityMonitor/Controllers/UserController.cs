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
        private async Task<IActionResult> CheckUserStatus()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return RedirectToAction("Login", "Account", new { area = "Identity" });
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return RedirectToAction("Login", "Account", new { area = "Identity" });
            if (user.LockoutEnd != null && user.LockoutEnd > DateTimeOffset.UtcNow)
            {
                await _userManager.UpdateSecurityStampAsync(user);
                await _userManager.UpdateAsync(user);
                await _signInManager.SignOutAsync();
                TempData["Error"] = "Tài khoản của bạn đã bị khóa!";
                return RedirectToAction("Index", "Login");
            }
            if (user.IsRestricted)
            {
                TempData["Warning"] = "Tài khoản của bạn đã bị hạn chế!";
                return RedirectToAction("Dashboard", "User");
            }
            return base.Ok();
        }

        private readonly IAlertService _alertService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;
        private readonly SignInManager<ApplicationUser> _signInManager;

        public UserController(IAlertService alertService, UserManager<ApplicationUser> userManager, ApplicationDbContext context, SignInManager<ApplicationUser> signInManager)
        {
            _alertService = alertService;
            _userManager = userManager;
            _context = context;
            _signInManager = signInManager;
        }

    public async Task<IActionResult> Index()
    {
        return RedirectToAction("Index", "Alerts");
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