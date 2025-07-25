using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecurityMonitor.Models;
using SecurityMonitor.Services.Interfaces;

namespace SecurityMonitor.Controllers
{
    [Authorize(Roles = "Admin")]
    public class IPBlockingController : Controller
    {
        private readonly IIPBlockingService _ipBlockingService;
        private readonly ILogger<IPBlockingController> _logger;

        public IPBlockingController(IIPBlockingService ipBlockingService, ILogger<IPBlockingController> logger)
        {
            _ipBlockingService = ipBlockingService;
            _logger = logger;
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
                await _ipBlockingService.BlockIPAsync(ip, reason, User.Identity?.Name ?? "Unknown");
                TempData["Success"] = $"IP {ip} has been blocked successfully";
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

        private bool IsValidIP(string ip)
        {
            return System.Net.IPAddress.TryParse(ip, out _);
        }
    }
}
