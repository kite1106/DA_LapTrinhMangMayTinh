using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecurityMonitor.DTOs;
using SecurityMonitor.Models;
using SecurityMonitor.Services;
using SecurityMonitor.Services.Interfaces;

namespace SecurityMonitor.Controllers;

[Authorize]
public class LogsController : Controller
{
    private readonly ILogService _logService;
    private readonly IAuditService _auditService;

    public LogsController(ILogService logService, IAuditService auditService)
    {
        _logService = logService;
        _auditService = auditService;
    }

    // Trang danh sách logs: Views/Logs/Index.cshtml
    public async Task<IActionResult> Index()
    {
        var logs = await _logService.GetAllLogsAsync();
        var dtoList = logs.Select(MapToDto).ToList();
        return View(dtoList);
    }

    // Chi tiết 1 log: Views/Logs/Details.cshtml
    public async Task<IActionResult> Details(long id)
    {
        var log = await _logService.GetLogByIdAsync(id);
        if (log == null) return NotFound();

        return View(MapToDto(log));
    }

    // Lọc theo nguồn
    public async Task<IActionResult> BySource(int sourceId)
    {
        var logs = await _logService.GetLogsBySourceAsync(sourceId);
        return View("Index", logs.Select(MapToDto).ToList());
    }

    // Lọc theo khoảng thời gian
    public async Task<IActionResult> ByDateRange(DateTime start, DateTime end)
    {
        var logs = await _logService.GetLogsByDateRangeAsync(start, end);
        return View("Index", logs.Select(MapToDto).ToList());
    }

    // Xử lý log
    [HttpPost]
    public async Task<IActionResult> Process(long id)
    {
        await _logService.ProcessLogAsync(id);
        return RedirectToAction("Details", new { id });
    }

    // Tạo mới (form nhập từ Razor View)
    [HttpPost]
    public async Task<IActionResult> Create(CreateLogDto createDto)
    {
        var log = new Log
        {
            LogSourceId = createDto.LogSourceId,
            EventType = createDto.EventType,
            Message = createDto.Message,
            RawData = createDto.RawData,
            IpAddress = createDto.IpAddress
        };

        log = await _logService.CreateLogAsync(log);
        return RedirectToAction("Details", new { id = log.Id });
    }

    private static LogDto MapToDto(Log log) => new(
        log.Id,
        log.Timestamp,
        log.LogSource.Name,
        log.EventType,
        log.Message,
        log.IpAddress,
        log.ProcessedAt
    );
}
