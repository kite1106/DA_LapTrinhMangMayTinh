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

        public async Task<IActionResult> Index(string? action, string? user, DateTime? startDate, DateTime? endDate)
        {
            IQueryable<AuditLog> query = _context.AuditLogs
                .Include(l => l.User)
                .OrderByDescending(l => l.Timestamp);

            // Filter theo action
            if (!string.IsNullOrEmpty(action) && action != "all")
            {
                query = query.Where(l => l.Action.ToLower() == action.ToLower());
            }

            // Filter theo user
            if (!string.IsNullOrEmpty(user) && user != "all")
            {
                query = query.Where(l => l.User != null && l.User.UserName.ToLower().Contains(user.ToLower()));
            }

            // Filter theo date range
            if (startDate.HasValue)
            {
                query = query.Where(l => l.Timestamp >= startDate.Value);
            }
            if (endDate.HasValue)
            {
                query = query.Where(l => l.Timestamp <= endDate.Value.AddDays(1));
            }

            var logs = await query
                .Take(1000)
                .ToListAsync();

            // Lấy danh sách actions và users cho filter
            ViewBag.Actions = await _context.AuditLogs
                .Select(l => l.Action)
                .Distinct()
                .OrderBy(a => a)
                .ToListAsync();
            
            ViewBag.Users = await _context.AuditLogs
                .Where(l => l.User != null)
                .Select(l => l.User.UserName)
                .Distinct()
                .OrderBy(u => u)
                .ToListAsync();

            ViewBag.SelectedAction = action;
            ViewBag.SelectedUser = user;
            ViewBag.StartDate = startDate;
            ViewBag.EndDate = endDate;

            return View(MapToAuditLogDtos(logs));
        }

        public async Task<IActionResult> UserActivity(string userId)
        {
            if (string.IsNullOrEmpty(userId))
            {
                return BadRequest("User ID is required");
            }

            var logs = await _auditService.GetUserActivityAsync(userId);
            var user = await _context.Users
                .Where(u => u.Id == userId)
                .Select(u => new { u.UserName, u.Email })
                .FirstOrDefaultAsync();

            ViewBag.UserName = user?.UserName ?? "Unknown";
            ViewBag.UserEmail = user?.Email;

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

        // API để lấy audit logs theo real-time
        [HttpGet]
        public async Task<IActionResult> GetRecentAuditLogs(int count = 50)
        {
            var logs = await _context.AuditLogs
                .Include(l => l.User)
                .OrderByDescending(l => l.Timestamp)
                .Take(count)
                .Select(l => new
                {
                    id = l.Id,
                    timestamp = l.Timestamp,
                    action = l.Action,
                    entityType = l.EntityType,
                    entityId = l.EntityId,
                    details = l.Details,
                    ipAddress = l.IpAddress,
                    username = l.User != null ? l.User.UserName : "Anonymous",
                    statusCode = l.StatusCode
                })
                .ToListAsync();

            return Json(new { success = true, logs = logs });
        }

        private static List<AuditLogDto> MapToAuditLogDtos(IEnumerable<AuditLog> logs)
        {
            return logs.Select(log => new AuditLogDto(
                Id: log.Id,
                Timestamp: log.Timestamp,
                UserEmail: log.User?.UserName ?? "Anonymous", // Hiển thị username thay vì email
                Action: log.Action,
                EntityType: log.EntityType,
                EntityId: log.EntityId,
                Details: log.Details,
                IpAddress: log.IpAddress
            )).ToList();
        }
    }
}
