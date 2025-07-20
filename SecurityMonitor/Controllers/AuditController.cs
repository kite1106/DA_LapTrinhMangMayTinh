using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecurityMonitor.DTOs;
using SecurityMonitor.Services;
using SecurityMonitor.Services.Interfaces;

namespace SecurityMonitor.Controllers;

[Authorize]
public class AuditController : Controller
{
    private readonly IAuditService _auditService;

    public AuditController(IAuditService auditService)
    {
        _auditService = auditService;
    }

    // Trả View hiển thị lịch sử hoạt động của user
    public async Task<IActionResult> UserActivity(string userId)
    {
        var logs = await _auditService.GetUserActivityAsync(userId);
        var dtoList = logs.Select(log => new AuditLogDto(
            log.Id,
            log.Timestamp,
            log.User?.UserName,
            log.Action,
            log.EntityType,
            log.EntityId,
            log.Details,
            log.IpAddress
        )).ToList();

        return View(dtoList); // View tại Views/Audit/UserActivity.cshtml
    }

    // Hiển thị log theo khoảng thời gian
    public async Task<IActionResult> DateRange(DateTime start, DateTime end)
    {
        var logs = await _auditService.GetActivityByDateRangeAsync(start, end);
        var dtoList = logs.Select(log => new AuditLogDto(
            log.Id,
            log.Timestamp,
            log.User?.UserName,
            log.Action,
            log.EntityType,
            log.EntityId,
            log.Details,
            log.IpAddress
        )).ToList();

        return View(dtoList); // View tại Views/Audit/DateRange.cshtml
    }
}
