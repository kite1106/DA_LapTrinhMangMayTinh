using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecurityMonitor.Data;
using SecurityMonitor.DTOs.Logs;
using SecurityMonitor.Models;
using SecurityMonitor.Services.Interfaces;

namespace SecurityMonitor.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AuditController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuditService _auditService;

        public AuditController(ApplicationDbContext context, IAuditService auditService)
        {
            _context = context;
            _auditService = auditService;
        }

        public async Task<IActionResult> Index()
        {
            var logs = await _context.AuditLogs
                .Include(l => l.User)
                .OrderByDescending(l => l.Timestamp)
                .Take(1000)
                .ToListAsync();

            return View(MapToAuditLogDtos(logs));
        }

        public async Task<IActionResult> UserActivity(string userId)
        {
            if (string.IsNullOrEmpty(userId))
            {
                return BadRequest("User ID is required");
            }

            var logs = await _auditService.GetUserActivityAsync(userId);
            ViewBag.UserEmail = await _context.Users
                .Where(u => u.Id == userId)
                .Select(u => u.Email)
                .FirstOrDefaultAsync();

            return View(MapToAuditLogDtos(logs));
        }

        public async Task<IActionResult> DateRange(DateTime start, DateTime end)
        {
            if (end < start)
            {
                return BadRequest("End date must be after start date");
            }

            if (end.Subtract(start).TotalDays > 90)
            {
                return BadRequest("Date range cannot exceed 90 days");
            }

            var logs = await _auditService.GetActivityByDateRangeAsync(start, end);
            return View("Index", MapToAuditLogDtos(logs));
        }

        private static List<AuditLogDto> MapToAuditLogDtos(IEnumerable<AuditLog> logs)
        {
            return logs.Select(log => new AuditLogDto(
                Id: log.Id,
                Timestamp: log.Timestamp,
                UserEmail: log.User?.Email,
                Action: log.Action,
                EntityType: log.EntityType,
                EntityId: log.EntityId,
                Details: log.Details,
                IpAddress: log.IpAddress
            )).ToList();
        }
    }
}
