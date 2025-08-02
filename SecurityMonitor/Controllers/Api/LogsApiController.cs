using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecurityMonitor.Models;
using SecurityMonitor.Services.Interfaces;

namespace SecurityMonitor.Controllers.Api;

[Authorize]
[ApiController]
[Route("api/logs")]
public class LogsApiController : ControllerBase
{
    private readonly ILogService _logService;
    private readonly ILogger<LogsApiController> _logger;

    public LogsApiController(ILogService logService, ILogger<LogsApiController> logger)
    {
        _logService = logService;
        _logger = logger;
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetLog(long id)
    {
        try
        {
            var log = await _logService.GetLogByIdAsync(id);
            if (log == null)
            {
                return NotFound(new { error = "Log not found" });
            }

            return Ok(new
            {
                id = log.Id,
                timestamp = log.Timestamp,
                logSource = log.LogSource?.Name,
                logLevelType = log.LogLevelType?.Name,
                ipAddress = log.IpAddress,
                userId = log.UserId,
                message = log.Message,
                details = log.Details,
                wasSuccessful = log.WasSuccessful
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving log {LogId}", id);
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteLog(long id)
    {
        try
        {
            await _logService.DeleteLogAsync(id);
            return Ok(new { message = "Log deleted successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting log {LogId}", id);
            return BadRequest(new { error = ex.Message });
        }
    }
} 