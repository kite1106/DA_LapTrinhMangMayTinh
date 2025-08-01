using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using SecurityMonitor.Models;
using SecurityMonitor.Services.Interfaces;
using SecurityMonitor.Hubs;

namespace SecurityMonitor.Controllers
{
    [Authorize(Roles = "Admin")]
    public class IPBlockingController : Controller
    {
        private readonly IIPBlockingService _ipBlockingService;
        private readonly ILogger<IPBlockingController> _logger;
        private readonly IHubContext<AlertHub> _alertHub;

        public IPBlockingController(
            IIPBlockingService ipBlockingService, 
            ILogger<IPBlockingController> logger,
            IHubContext<AlertHub> alertHub)
        {
            _ipBlockingService = ipBlockingService;
            _logger = logger;
            _alertHub = alertHub;
        }

        public async Task<IActionResult> Index()
        {
            var blockedIPs = await _ipBlockingService.GetBlockedIPsAsync();
            return View(blockedIPs);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Block(string ip, string reason)
        {
            if (string.IsNullOrEmpty(ip) || !IsValidIP(ip))
            {
                TempData["Error"] = "Invalid IP address format";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                var blockedIP = await _ipBlockingService.BlockIPAsync(ip, reason, User.Identity?.Name ?? "Unknown");
                TempData["Success"] = $"IP {ip} has been blocked successfully";
                
                // Gửi thông báo real-time
                await _alertHub.Clients.All.SendAsync("ReceiveAlert", new
                {
                    title = "IP Address Blocked",
                    description = $"IP address {ip} has been blocked. Reason: {reason}",
                    sourceIp = ip,
                    timestamp = DateTime.UtcNow,
                    severityLevel = "High",
                    type = "IP Blocking"
                });
                
                _logger.LogInformation("IP {IP} blocked by {Admin} with reason: {Reason}", 
                    ip, User.Identity?.Name, reason);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error blocking IP {IP}", ip);
                TempData["Error"] = "An error occurred while blocking the IP";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Unblock(string ip)
        {
            try
            {
                var result = await _ipBlockingService.UnblockIPAsync(ip);
                if (result)
                {
                    TempData["Success"] = $"IP {ip} has been unblocked successfully";
                    
                    // Gửi thông báo real-time
                    await _alertHub.Clients.All.SendAsync("ReceiveAlert", new
                    {
                        title = "IP Address Unblocked",
                        description = $"IP address {ip} has been unblocked",
                        sourceIp = ip,
                        timestamp = DateTime.UtcNow,
                        severityLevel = "Info",
                        type = "IP Unblocking"
                    });
                    
                    _logger.LogInformation("IP {IP} unblocked by {Admin}", ip, User.Identity?.Name);
                }
                else
                {
                    TempData["Error"] = $"IP {ip} was not found in the blocked list";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unblocking IP {IP}", ip);
                TempData["Error"] = "An error occurred while unblocking the IP";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Details(string ip)
        {
            var details = await _ipBlockingService.GetBlockedIPDetailsAsync(ip);
            if (details == null)
            {
                return NotFound();
            }
            return View(details);
        }

        [HttpPost]
        public async Task<IActionResult> BlockAjax(string ip, string reason)
        {
            if (string.IsNullOrEmpty(ip) || !IsValidIP(ip))
            {
                return BadRequest(new { message = "Invalid IP address format" });
            }

            try
            {
                var blockedIP = await _ipBlockingService.BlockIPAsync(ip, reason, User.Identity?.Name ?? "Unknown");
                
                // Gửi thông báo real-time
                await _alertHub.Clients.All.SendAsync("ReceiveAlert", new
                {
                    title = "IP Address Blocked",
                    description = $"IP address {ip} has been blocked. Reason: {reason}",
                    sourceIp = ip,
                    timestamp = DateTime.UtcNow,
                    severityLevel = "High",
                    type = "IP Blocking"
                });
                
                _logger.LogInformation("IP {IP} blocked via AJAX by {Admin} with reason: {Reason}", 
                    ip, User.Identity?.Name, reason);
                
                return Ok(new { message = $"IP {ip} has been blocked successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error blocking IP {IP} via AJAX", ip);
                return StatusCode(500, new { message = "An error occurred while blocking the IP" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> UnblockAjax(string ip)
        {
            try
            {
                var result = await _ipBlockingService.UnblockIPAsync(ip);
                if (result)
                {
                    // Gửi thông báo real-time
                    await _alertHub.Clients.All.SendAsync("ReceiveAlert", new
                    {
                        title = "IP Address Unblocked",
                        description = $"IP address {ip} has been unblocked",
                        sourceIp = ip,
                        timestamp = DateTime.UtcNow,
                        severityLevel = "Info",
                        type = "IP Unblocking"
                    });
                    
                    _logger.LogInformation("IP {IP} unblocked via AJAX by {Admin}", ip, User.Identity?.Name);
                    
                    return Ok(new { message = $"IP {ip} has been unblocked successfully" });
                }
                else
                {
                    return BadRequest(new { message = $"IP {ip} was not found in the blocked list" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unblocking IP {IP} via AJAX", ip);
                return StatusCode(500, new { message = "An error occurred while unblocking the IP" });
            }
        }

        private bool IsValidIP(string ip)
        {
            return System.Net.IPAddress.TryParse(ip, out _);
        }
    }
}
