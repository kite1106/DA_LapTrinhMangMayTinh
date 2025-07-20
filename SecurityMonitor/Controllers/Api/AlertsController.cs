using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecurityMonitor.DTOs;
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
    public async Task<ActionResult<IEnumerable<AlertDto>>> GetAlerts()
    {
        try
        {
            var alerts = await _alertService.GetAllAlertsAsync();
            return Ok(alerts.Select(MapToDto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving alerts");
            return StatusCode(500, "Internal server error");
        }
    }

    // GET: api/alerts/5
    [HttpGet("{id}")]
    public async Task<ActionResult<AlertDto>> GetAlert(int id)
    {
        try
        {
            var alert = await _alertService.GetAlertByIdAsync(id);
            if (alert == null)
            {
                return NotFound();
            }
            return Ok(MapToDto(alert));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving alert {AlertId}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    // POST: api/alerts
    [HttpPost]
    public async Task<ActionResult<AlertDto>> CreateAlert([FromBody] CreateAlertDto createAlertDto)
    {
        try
        {
            var alert = new Alert
            {
                Title = createAlertDto.Title,
                Description = createAlertDto.Description,
                AlertTypeId = createAlertDto.AlertTypeId,
                SeverityLevelId = createAlertDto.SeverityLevelId,
                StatusId = (int)AlertStatusId.New,
                SourceIp = createAlertDto.SourceIp,
                TargetIp = createAlertDto.TargetIp,
                LogId = createAlertDto.LogId,
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

            // Get status ID from name
            var status = await _alertService.GetAlertStatusByNameAsync(updateStatusDto.Status);
            if (status == null)
            {
                return BadRequest("Invalid status");
            }

            alert.StatusId = status.Id;
            alert.ResolvedAt = status.IsTerminal ? DateTime.UtcNow : null;
            alert.ResolvedById = status.IsTerminal ? User.Identity?.Name : null;

            var updatedAlert = await _alertService.UpdateAlertAsync(id, alert);
            if (updatedAlert == null)
            {
                return StatusCode(500, "Failed to update alert");
            }
            return Ok(MapToDto(updatedAlert));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating alert status {AlertId}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    private static AlertDto MapToDto(Alert alert) => new(
        alert.Id,
        alert.Timestamp,
        alert.Title,
        alert.Description,
        alert.AlertType?.Name ?? "Unknown",
        alert.SeverityLevel?.Name ?? "Unknown",
        alert.Status?.Name ?? "Unknown",
        alert.SourceIp,
        alert.TargetIp,
        alert.AssignedTo?.UserName,
        alert.ResolvedBy?.UserName,
        alert.ResolvedAt,
        alert.Resolution
    );
}
