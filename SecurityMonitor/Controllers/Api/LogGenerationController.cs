using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecurityMonitor.Services.Interfaces;

namespace SecurityMonitor.Controllers.Api
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin")]
    public class LogGenerationController : ControllerBase
    {
        private readonly ILogGenerationControlService _logControlService;
        private readonly ILogger<LogGenerationController> _logger;

        public LogGenerationController(
            ILogGenerationControlService logControlService,
            ILogger<LogGenerationController> logger)
        {
            _logControlService = logControlService;
            _logger = logger;
        }

        [HttpGet("status")]
        public async Task<IActionResult> GetStatus()
        {
            try
            {
                var isEnabled = await _logControlService.GetLogGenerationStatusAsync();
                return Ok(new { 
                    isEnabled = isEnabled,
                    message = isEnabled ? "Log generation is enabled" : "Log generation is disabled"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting log generation status");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        [HttpPost("enable")]
        public async Task<IActionResult> Enable()
        {
            try
            {
                var result = await _logControlService.EnableLogGenerationAsync();
                if (result)
                {
                    return Ok(new { 
                        success = true,
                        message = "Log generation enabled successfully"
                    });
                }
                else
                {
                    return BadRequest(new { 
                        success = false,
                        message = "Failed to enable log generation"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error enabling log generation");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        [HttpPost("disable")]
        public async Task<IActionResult> Disable()
        {
            try
            {
                var result = await _logControlService.DisableLogGenerationAsync();
                if (result)
                {
                    return Ok(new { 
                        success = true,
                        message = "Log generation disabled successfully"
                    });
                }
                else
                {
                    return BadRequest(new { 
                        success = false,
                        message = "Failed to disable log generation"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disabling log generation");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        [HttpPost("toggle")]
        public async Task<IActionResult> Toggle()
        {
            try
            {
                var result = await _logControlService.ToggleLogGenerationAsync();
                var newStatus = await _logControlService.GetLogGenerationStatusAsync();
                
                return Ok(new { 
                    success = true,
                    isEnabled = newStatus,
                    message = newStatus ? "Log generation enabled" : "Log generation disabled"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling log generation");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }
    }
} 