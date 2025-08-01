using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using SecurityMonitor.Data;
using SecurityMonitor.DTOs.Alerts;
using SecurityMonitor.Models;
using SecurityMonitor.Services.Interfaces;
using SecurityMonitor.Hubs;

namespace SecurityMonitor.Controllers.Api;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class AlertsController : ControllerBase
{
    private readonly IAlertService _alertService;
    private readonly ILogger<AlertsController> _logger;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _context;
    private readonly IHubContext<AlertHub> _hubContext;

    public AlertsController(
        IAlertService alertService, 
        ILogger<AlertsController> logger,
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext context,
        IHubContext<AlertHub> hubContext)
    {
        _alertService = alertService;
        _logger = logger;
        _userManager = userManager;
        _context = context;
        _hubContext = hubContext;
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
            alert.Resolution = updateStatusDto.Resolution;

            await _alertService.UpdateAlertAsync(alert);

            // Notify via SignalR
            await _hubContext.Clients.Group("Alerts").SendAsync("ReceiveAlertUpdate", new
            {
                id = alert.Id,
                status = alert.Status?.Name,
                resolvedAt = alert.ResolvedAt,
                resolution = alert.Resolution
            });

            return Ok(new { message = "Alert status updated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating alert status {AlertId}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    // POST: api/alerts/{id}/process
    [HttpPost("{id}/process")]
    public async Task<IActionResult> ProcessAlert(int id, [FromBody] ProcessAlertDto processDto)
    {
        try
        {
            var alert = await _alertService.GetAlertByIdAsync(id);
            if (alert == null)
            {
                return NotFound();
            }

            // Process based on action type
            switch (processDto.Action.ToLower())
            {
                case "blockip":
                    await BlockIP(alert.SourceIp ?? "unknown", processDto.Reason);
                    break;
                case "blockaccount":
                    await BlockAccount(alert.SourceIp ?? "unknown", processDto.Reason);
                    break;
                case "restrictaccount":
                    await RestrictAccount(alert.SourceIp ?? "unknown", processDto.Reason);
                    break;
                default:
                    return BadRequest("Invalid action type");
            }

            // Update alert status to InProgress
            alert.StatusId = (int)AlertStatusId.InProgress;
            await _alertService.UpdateAlertAsync(alert);

            return Ok(new { message = "Alert processed successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing alert {AlertId}", id);
            return StatusCode(500, "Internal server error");
        }
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
            _logger.LogError(ex, "Error updating alert {AlertId}", id);
            return BadRequest(new { error = ex.Message });
        }
    }

    private async Task BlockIP(string ipAddress, string reason)
    {
        // TODO: Implement IP blocking logic
        _logger.LogInformation("Blocking IP: {IP} - Reason: {Reason}", ipAddress, reason);
        
        // Notify clients via SignalR to block IP
        await _hubContext.Clients.All.SendAsync("BlockIP", ipAddress, reason);
    }

    private async Task BlockAccount(string ipAddress, string reason)
    {
        // TODO: Implement account blocking logic
        _logger.LogInformation("Blocking account for IP: {IP} - Reason: {Reason}", ipAddress, reason);
        
        // Find user by IP address and block their account
        var user = await _userManager.Users.FirstOrDefaultAsync(u => u.LastLoginIP == ipAddress);
        if (user != null)
        {
            user.IsActive = false;
            user.LockoutEnd = DateTimeOffset.MaxValue;
            await _userManager.UpdateAsync(user);
            
            // Notify client to logout immediately
            await _hubContext.Clients.User(user.Id).SendAsync("ForceLogout", "Tài khoản của bạn đã bị chặn do vi phạm bảo mật.");
        }
    }

    private async Task RestrictAccount(string ipAddress, string reason)
    {
        // TODO: Implement account restriction logic
        _logger.LogInformation("Restricting account for IP: {IP} - Reason: {Reason}", ipAddress, reason);
        
        // Find user by IP address and restrict their account
        var user = await _userManager.Users.FirstOrDefaultAsync(u => u.LastLoginIP == ipAddress);
        if (user != null)
        {
                         // Add restriction record
             var restriction = new AccountRestriction
             {
                 UserId = user.Id,
                 RestrictedBy = User.Identity?.Name ?? "System",
                 RestrictionType = "Temporary",
                 Reason = reason,
                 StartTime = DateTimeOffset.UtcNow,
                 EndTime = DateTimeOffset.UtcNow.AddHours(24), // 24 hours restriction
                 IsActive = true,
                 Notes = $"Restricted due to security alert: {reason}"
             };
            
            _context.AccountRestrictions.Add(restriction);
            await _context.SaveChangesAsync();
            
            // Notify client to redirect to user dashboard
            await _hubContext.Clients.User(user.Id).SendAsync("RedirectToUserDashboard", "Tài khoản của bạn đã bị hạn chế. Vui lòng liên hệ admin.");
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
