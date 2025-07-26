using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

        [HttpPut("{id}/status")]
        public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateAlertStatusDto dto)
        {
            try
            {
                var alert = await _alertService.GetAlertByIdAsync(id);
                if (alert == null)
                {
                    return NotFound();
                }

                // Use the AlertStatusId directly from DTO
                var statusId = dto.Status;

                alert.StatusId = (int)statusId;
                if (statusId == AlertStatusId.Resolved)
                {
                    alert.ResolvedAt = DateTime.UtcNow;
                    alert.ResolvedById = User.Identity?.Name;
                }

                await _alertService.UpdateAlertAsync(alert.Id, alert);
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating alert status for alert {AlertId}", id);
                return StatusCode(500, "Có lỗi xảy ra khi cập nhật trạng thái cảnh báo");
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
