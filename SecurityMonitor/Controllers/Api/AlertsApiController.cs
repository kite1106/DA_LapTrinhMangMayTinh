using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecurityMonitor.Data;
using SecurityMonitor.DTOs.Alerts;
using SecurityMonitor.Models;
using SecurityMonitor.Services.Interfaces;

namespace SecurityMonitor.Controllers.Api
{
    [Route("api/alerts")]
    [ApiController]
    [Authorize]
    public class AlertsApiController : ControllerBase
    {
        private readonly IAlertService _alertService;
        private readonly ILogger<AlertsApiController> _logger;

        public AlertsApiController(IAlertService alertService, ILogger<AlertsApiController> logger)
        {
            _alertService = alertService;
            _logger = logger;
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<AlertDetailDto>> GetAlert(int id)
        {
            var alert = await _alertService.GetAlertByIdAsync(id);
            if (alert == null)
            {
                return NotFound();
            }

            return MapToDto(alert);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateAlert(int id, [FromBody] AlertUpdateDto updateDto)
        {
            try
            {
                var alert = await _alertService.GetAlertByIdAsync(id);
                if (alert == null)
                {
                    return NotFound(new { error = "Alert not found" });
                }

                // Update alert properties
                alert.Title = updateDto.Title ?? alert.Title;
                alert.Description = updateDto.Description ?? alert.Description;
                alert.SeverityLevelId = updateDto.SeverityLevelId ?? alert.SeverityLevelId;
                alert.StatusId = updateDto.StatusId ?? alert.StatusId;

                await _alertService.UpdateAlertAsync(alert);

                return Ok(new { message = "Alert updated successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating alert {Id}", id);
                return BadRequest(new { error = ex.Message });
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
}
