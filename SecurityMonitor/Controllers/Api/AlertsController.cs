using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecurityMonitor.DTOs.Alerts;
using SecurityMonitor.Models;
using SecurityMonitor.Services.Interfaces;

namespace SecurityMonitor.Controllers.Api;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class AlertsController : ControllerBase
{
    private readonly IAlertService _alertService;
    private readonly ILogger<AlertsController> _logger;

    public AlertsController(IAlertService alertService, ILogger<AlertsController> logger)
    {
        _alertService = alertService;
        _logger = logger;
    }

    // GET: api/alerts
    [HttpGet]
    public async Task<ActionResult<IEnumerable<AlertListDto>>> GetAlerts()
    {
        try
        {
            var alerts = await _alertService.GetAllAlertsAsync();
            return Ok(alerts.Select(alert => new AlertListDto
            {
                Id = alert.Id,
                Timestamp = alert.Timestamp,
                Title = alert.Title,
                Description = alert.Description,
                SourceIp = alert.SourceIp ?? string.Empty,
                SeverityLevel = alert.SeverityLevel?.Name ?? "Unknown",
                Status = alert.Status?.Name ?? "Unknown",
                AssignedTo = alert.AssignedTo?.UserName
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving alerts");
            return StatusCode(500, "Internal server error");
        }
    }

    // GET: api/alerts/5
    [HttpGet("{id}")]
    public async Task<ActionResult<AlertDetailDto>> GetAlert(int id)
    {
        try
        {
            var alert = await _alertService.GetAlertByIdAsync(id);
            if (alert == null)
            {
                return NotFound();
            }
            return Ok(new AlertDetailDto
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
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving alert {AlertId}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    // POST: api/alerts
    [HttpPost]
    public async Task<ActionResult<AlertListDto>> CreateAlert([FromBody] CreateAlertDto createAlertDto)
    {
        try
        {
            var alert = new Alert
            {
                Title = createAlertDto.Title,
                Description = createAlertDto.Description,
                AlertTypeId = (int)createAlertDto.AlertTypeId,
                SeverityLevelId = (int)createAlertDto.SeverityLevelId,
                StatusId = (int)AlertStatusId.New,
                SourceIp = createAlertDto.SourceIp,
                TargetIp = createAlertDto.TargetIp,
                Timestamp = DateTime.UtcNow
            };

            var createdAlert = await _alertService.CreateAlertAsync(alert);
            return CreatedAtAction(
                nameof(GetAlert),
                new { id = createdAlert.Id },
                MapToDto(createdAlert));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating alert");
            return StatusCode(500, "Internal server error");
        }
    }

    // PUT: api/alerts/5/status
    [HttpPut("{id}/status")]
    public async Task<IActionResult> UpdateAlertStatus(int id, [FromBody] UpdateAlertStatusDto updateStatusDto)
    {
        try
        {
            var alert = await _alertService.GetAlertByIdAsync(id);
            if (alert == null)
            {
                return NotFound();
            }

            alert.StatusId = (int)updateStatusDto.Status;
            alert.ResolvedAt = updateStatusDto.Status == AlertStatusId.Resolved ? DateTime.UtcNow : null;
            alert.ResolvedById = updateStatusDto.Status == AlertStatusId.Resolved ? User.Identity?.Name : null;

            var updatedAlert = await _alertService.UpdateAlertAsync(id, alert);
            if (updatedAlert == null)
            {
                return StatusCode(500, "Failed to update alert");
            }
            
            // Get the alert with all the required navigation properties
            var alertWithDetails = await _alertService.GetAlertByIdAsync(id);
            return Ok(new AlertDetailDto
            {
                Id = updatedAlert.Id,
                Timestamp = updatedAlert.Timestamp,
                Title = updatedAlert.Title,
                Description = updatedAlert.Description,
                AlertType = alertWithDetails?.AlertType?.Name ?? string.Empty,
                SeverityLevel = alertWithDetails?.SeverityLevel?.Name ?? string.Empty,
                Status = alertWithDetails?.Status?.Name ?? string.Empty,
                SourceIp = updatedAlert.SourceIp,
                TargetIp = updatedAlert.TargetIp,
                AssignedTo = updatedAlert.AssignedTo?.UserName,
                ResolvedBy = updatedAlert.ResolvedBy?.UserName,
                ResolvedAt = updatedAlert.ResolvedAt,
                Resolution = updatedAlert.Resolution
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating alert status {AlertId}", id);
            return StatusCode(500, "Internal server error");
        }
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
