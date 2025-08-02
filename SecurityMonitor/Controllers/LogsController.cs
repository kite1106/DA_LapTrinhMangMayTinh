using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecurityMonitor.Data;
using SecurityMonitor.DTOs.Logs;
using SecurityMonitor.Models;
using SecurityMonitor.Services.Interfaces;

namespace SecurityMonitor.Controllers;

[Authorize]
public class LogsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IAuditService _auditService;

    public LogsController(ApplicationDbContext context, IAuditService auditService)
    {
        _context = context;
        _auditService = auditService;
    }

    // Trang danh sách logs: Views/Logs/Index.cshtml
    public async Task<IActionResult> Index(string? source, string? level, DateTime? startDate, DateTime? endDate)
    {
        IQueryable<LogEntry> query = _context.LogEntries
            .Include(l => l.LogSource)
            .Include(l => l.LogLevelType)
            .OrderByDescending(l => l.Timestamp);

        // Filter theo source
        if (!string.IsNullOrEmpty(source) && source != "all")
        {
            query = query.Where(l => l.LogSource.Name.ToLower() == source.ToLower());
        }

        // Filter theo level
        if (!string.IsNullOrEmpty(level) && level != "all")
        {
            query = query.Where(l => l.LogLevelType.Name.ToLower() == level.ToLower());
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
            .Select(l => new MyLogDto(
                l.Timestamp,
                l.IpAddress,
                l.LogSource.Name,
                l.LogLevelType != null ? l.LogLevelType.Name : "Information",
                l.WasSuccessful,
                l.Message,
                l.UserId ?? "System", // Hiển thị UserId hoặc System
                l.Details
            ))
            .Take(500) // Giới hạn 500 records để tránh quá tải
            .ToListAsync();

        // Lấy danh sách sources và levels cho filter
        ViewBag.Sources = await _context.LogSources.Select(s => s.Name).ToListAsync();
        ViewBag.Levels = await _context.LogLevelTypes.Select(l => l.Name).ToListAsync();
        ViewBag.SelectedSource = source;
        ViewBag.SelectedLevel = level;
        ViewBag.StartDate = startDate;
        ViewBag.EndDate = endDate;

        return View(logs);
    }

    // Chi tiết 1 log: Views/Logs/Details.cshtml
    public async Task<IActionResult> Details(long id)
    {
        var log = await _context.LogEntries
            .Include(l => l.LogSource)
            .Include(l => l.LogLevelType)
            .Include(l => l.LogAnalyses)
            .Include(l => l.Alerts)
            .FirstOrDefaultAsync(l => l.Id == id);
            
        if (log == null) return NotFound();

        return View(MapToDto(log));
    }

    // Xử lý log
    [HttpPost]
    public async Task<IActionResult> Process(long id)
    {
        var log = await _context.LogEntries.FindAsync(id);
        if (log == null) return NotFound();

        log.ProcessedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        // Ghi audit log
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrEmpty(userId))
        {
            await _auditService.LogActivityAsync(
                userId: userId,
                action: "ProcessLog",
                entityType: "Log",
                entityId: id.ToString(),
                details: "Đã xử lý log");
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    // API để lấy logs theo real-time
    [HttpGet]
    public async Task<IActionResult> GetRecentLogs(int count = 50)
    {
        var logs = await _context.LogEntries
            .Include(l => l.LogSource)
            .Include(l => l.LogLevelType)
            .OrderByDescending(l => l.Timestamp)
            .Take(count)
            .Select(l => new
            {
                id = l.Id,
                timestamp = l.Timestamp,
                message = l.Message,
                level = l.LogLevelType != null ? l.LogLevelType.Name : "Information",
                source = l.LogSource != null ? l.LogSource.Name : "Unknown",
                ipAddress = l.IpAddress,
                username = l.UserId ?? "System",
                wasSuccessful = l.WasSuccessful,
                isProcessed = l.ProcessedAt.HasValue
            })
            .ToListAsync();

        return Json(new { success = true, logs = logs });
    }

    private static LogDto MapToDto(LogEntry log)
    {
        return new LogDto(
            Id: log.Id,
            Timestamp: log.Timestamp,
            IpAddress: log.IpAddress,
            Message: log.Message ?? string.Empty,
            Level: log.LogLevelType?.Name ?? "Information",
            SourceName: log.LogSource?.Name ?? "Unknown",
            Details: log.Details,
            IsProcessed: log.ProcessedAt.HasValue,
            ProcessedAt: log.ProcessedAt,
            Username: log.UserId ?? "System", // Hiển thị UserId
            WasSuccessful: log.WasSuccessful
        );
    }
}
