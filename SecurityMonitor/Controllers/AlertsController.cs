using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecurityMonitor.DTOs;
using SecurityMonitor.Models;
using SecurityMonitor.Services;
using SecurityMonitor.Services.Interfaces;

namespace SecurityMonitor.Controllers;

[Authorize]
public class AlertsController : Controller
{
    private readonly IAlertService _alertService;

    public AlertsController(IAlertService alertService)
    {
        _alertService = alertService;
    }

    public async Task<IActionResult> Index()
    {
        var alerts = await _alertService.GetAllAlertsAsync();
        var alertDtos = alerts.Select(MapToDto).ToList();
        return View(alertDtos); // → View tại Views/Alerts/Index.cshtml
    }

    public async Task<IActionResult> Details(int id)
    {
        var alert = await _alertService.GetAlertByIdAsync(id);
        if (alert == null) return NotFound();

        return View(MapToDto(alert)); // → View tại Views/Alerts/Details.cshtml
    }
    

    [HttpPost]
    public async Task<IActionResult> Assign(int id)
    {
        var userId = User.Identity?.Name;
        if (userId == null) return Unauthorized();

        var updated = await _alertService.AssignAlertAsync(id, userId);
        if (updated == null) return NotFound();

        return RedirectToAction("Index"); // hoặc "Details", tùy bạn
    }

    [HttpPost]
    public async Task<IActionResult> Resolve(int id, string resolution)
    {
        var userId = User.Identity?.Name;
        if (userId == null) return Unauthorized();

        var resolved = await _alertService.ResolveAlertAsync(id, userId, resolution);
        if (resolved == null) return NotFound();

        return RedirectToAction("Index");
    }

    private static AlertDto MapToDto(Alert alert) => new(
        alert.Id,
        alert.Timestamp,
        alert.Title,
        alert.Description,
        alert.AlertType.Name,
        alert.SeverityLevel.Name,
        alert.Status.Name,
        alert.SourceIp,
        alert.TargetIp,
        alert.AssignedTo?.UserName,
        alert.ResolvedBy?.UserName,
        alert.ResolvedAt,
        alert.Resolution
    );
}
