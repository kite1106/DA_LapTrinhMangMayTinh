using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecurityMonitor.DTOs.Alerts;
using SecurityMonitor.Models;
using SecurityMonitor.Services;
using SecurityMonitor.Services.Interfaces;

namespace SecurityMonitor.Controllers;

[Authorize]
public class AlertsController : Controller
{
    private readonly IAlertService _alertService;
    private readonly IIPBlockingService _ipBlockingService;
    private readonly ILogEventService _logEventService;
    private readonly ILogger<AlertsController> _logger;

    public AlertsController(
        IAlertService alertService,
        IIPBlockingService ipBlockingService,
        ILogEventService logEventService,
        ILogger<AlertsController> logger)
    {
        _alertService = alertService;
        _ipBlockingService = ipBlockingService;
        _logEventService = logEventService;
        _logger = logger;
    }

    public async Task<IActionResult> Index()
    {
        try
        {
            IEnumerable<Alert> alerts;
            if (User.IsInRole("Admin") || User.IsInRole("Analyst"))
            {
                alerts = await _alertService.GetAllAlertsAsync();
            }
            else
            {
                var userId = User.Identity?.Name;
                if (userId == null)
                {
                    return Unauthorized();
                }
                alerts = await _alertService.GetUserAlertsAsync(userId);
            }
            
            var alertDtos = alerts.Select(alert => new AlertListDto
            {
                Id = alert.Id,
                Timestamp = alert.Timestamp,
                Title = alert.Title,
                Description = alert.Description,
                SourceIp = alert.SourceIp ?? string.Empty,
                SeverityLevel = alert.SeverityLevel?.Name ?? string.Empty,
                Status = alert.Status?.Name ?? string.Empty,
                AssignedTo = alert.AssignedTo?.UserName
            }).ToList();
            
            return View(alertDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving alerts");
            return View("Error");
        }
    }

    public async Task<IActionResult> Details(int id)
    {
        try
        {
            var alert = await _alertService.GetAlertByIdAsync(id);
            if (alert == null)
            {
                return NotFound();
            }
            
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return PartialView("_AlertDetails", MapToDto(alert));
            }
            
            return View(MapToDto(alert));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving alert {AlertId}", id);
            return View("Error");
        }
    }

    // GET: Alerts/Create
    public async Task<IActionResult> Create()
    {
        try
        {
            ViewBag.AlertTypes = await _alertService.GetAllAlertTypesAsync();
            ViewBag.SeverityLevels = await _alertService.GetAllSeverityLevelsAsync();
            return View();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading alert types and severity levels");
            return View("Error");
        }
    }

    // POST: Alerts/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateAlertDto dto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                ViewBag.AlertTypes = await _alertService.GetAllAlertTypesAsync();
                ViewBag.SeverityLevels = await _alertService.GetAllSeverityLevelsAsync();
                return View(dto);
            }

            var alert = new Alert
            {
                Title = dto.Title,
                Description = dto.Description,
                AlertTypeId = (int)dto.AlertTypeId,
                SeverityLevelId = (int)dto.SeverityLevelId,
                StatusId = (int)AlertStatusId.New,
                SourceIp = dto.SourceIp,
                TargetIp = dto.TargetIp,
                Timestamp = DateTime.UtcNow
            };

            var createdAlert = await _alertService.CreateAlertAsync(alert);

            // Nếu là cảnh báo nghiêm trọng và có IP nguồn, tự động chặn IP
            if (dto.SeverityLevelId == SeverityLevelId.Critical && !string.IsNullOrEmpty(dto.SourceIp))
            {
                try
                {
                    await _ipBlockingService.BlockIPAsync(
                        dto.SourceIp,
                        $"Tự động chặn từ cảnh báo nghiêm trọng: {dto.Title}",
                        "System"
                    );
                    _logger.LogInformation("Đã tự động chặn IP {IP} từ cảnh báo nghiêm trọng", dto.SourceIp);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi khi tự động chặn IP {IP}", dto.SourceIp);
                }
            }

            return RedirectToAction(nameof(Details), new { id = createdAlert.Id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating alert");
            return View("Error");
        }
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

        var alert = await _alertService.GetAlertByIdAsync(id);
        if (alert == null) return NotFound();

        // Log việc xử lý alert
        await _logEventService.RecordSystemEventAsync(
            "AlertResolution",
            $"Alert {id} resolved by {userId}",
            "AlertSystem",
            alert.SourceIp
        );

        // Nếu là alert từ IP đáng ngờ, ghi nhận hành động
        if (alert.SourceIp != null)
        {
            await _logEventService.RecordSuspiciousEventAsync(
                "AlertResolution",
                $"Alert về IP {alert.SourceIp} đã được xử lý: {resolution}",
                alert.SourceIp,
                userId
            );
        }

        var resolved = await _alertService.ResolveAlertAsync(id, userId, resolution);
        if (resolved == null) return NotFound();

        return RedirectToAction("Index");
    }

    private static AlertDetailDto MapToDto(Alert alert) => new AlertDetailDto
    {
        Id = alert.Id,
        Timestamp = alert.Timestamp,
        Title = alert.Title,
        Description = alert.Description,
        AlertType = alert.AlertType?.Name ?? "Unknown",
        SeverityLevel = alert.SeverityLevel?.Name ?? "Unknown",
        Status = alert.Status?.Name ?? "Unknown",
        SourceIp = alert.SourceIp,
        TargetIp = alert.TargetIp,
        AssignedTo = alert.AssignedTo?.UserName,
        ResolvedBy = alert.ResolvedBy?.UserName,
        ResolvedAt = alert.ResolvedAt,
        Resolution = alert.Resolution
    };
}
