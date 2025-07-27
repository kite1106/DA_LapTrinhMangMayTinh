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
    public async Task<IActionResult> Index(string? source)
    {
        IQueryable<Log> query = _context.Logs
            .Include(l => l.LogSource)
            .OrderByDescending(l => l.Timestamp);

        if (!string.IsNullOrEmpty(source) && source != "all")
        {
            query = query.Where(l => l.LogSource.Name.ToLower() == source.ToLower());
        }

        var logs = await query
            .Select(l => new MyLogDto(
                l.Timestamp,
                l.IpAddress,
                l.LogSource.Name,
                l.EventType ?? "Unknown",
                l.ProcessedAt.HasValue,
                l.Message
            ))
            .ToListAsync();

        return View(logs);
    }

    // Chi tiết 1 log: Views/Logs/Details.cshtml
    public async Task<IActionResult> Details(long id)
    {
        var log = await _context.Logs
            .Include(l => l.LogSource)
            .FirstOrDefaultAsync(l => l.Id == id);
            
        if (log == null) return NotFound();

        return View(MapToDto(log));
    }

    // Xử lý log
    [HttpPost]
    public async Task<IActionResult> Process(long id)
    {
        var log = await _context.Logs.FindAsync(id);
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

    private static LogDto MapToDto(Log log)
    {
        return new LogDto(
            Id: log.Id,
            Timestamp: log.Timestamp,
            IpAddress: log.IpAddress,
            Message: log.Message ?? string.Empty,
            Level: log.EventType ?? "Information",
            SourceName: log.LogSource?.Name ?? "Unknown",
            Details: log.RawData,
            IsProcessed: log.ProcessedAt.HasValue,
            ProcessedAt: log.ProcessedAt
        );
    }
}
