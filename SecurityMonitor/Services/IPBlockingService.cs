using Microsoft.EntityFrameworkCore;
using SecurityMonitor.Data;
using SecurityMonitor.Models;
using SecurityMonitor.Services.Interfaces;

namespace SecurityMonitor.Services
{
    public class IPBlockingService : IIPBlockingService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<IPBlockingService> _logger;

        public IPBlockingService(ApplicationDbContext context, ILogger<IPBlockingService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<IEnumerable<BlockedIP>> GetBlockedIPsAsync()
        {
            return await _context.BlockedIPs
                .OrderByDescending(b => b.BlockedAt)
                .ToListAsync();
        }

        public async Task<BlockedIP> BlockIPAsync(string ip, string reason, string blockedBy)
        {
            var existingBlock = await _context.BlockedIPs.FirstOrDefaultAsync(b => b.IpAddress == ip);
            if (existingBlock != null)
            {
                _logger.LogWarning("IP {IP} is already blocked", ip);
                return existingBlock;
            }

            var blockedIP = new BlockedIP
            {
                IpAddress = ip,
                Reason = reason,
                BlockedBy = blockedBy,
                BlockedAt = DateTime.UtcNow
            };

            _context.BlockedIPs.Add(blockedIP);
            await _context.SaveChangesAsync();

            _logger.LogInformation("IP {IP} has been blocked by {BlockedBy}", ip, blockedBy);
            return blockedIP;
        }

        public async Task<bool> UnblockIPAsync(string ip)
        {
            var blockedIP = await _context.BlockedIPs.FirstOrDefaultAsync(b => b.IpAddress == ip);
            if (blockedIP == null)
            {
                _logger.LogWarning("Attempted to unblock non-blocked IP {IP}", ip);
                return false;
            }

            _context.BlockedIPs.Remove(blockedIP);
            await _context.SaveChangesAsync();

            _logger.LogInformation("IP {IP} has been unblocked", ip);
            return true;
        }

        public async Task<bool> IsIPBlockedAsync(string ip)
        {
            return await _context.BlockedIPs.AnyAsync(b => b.IpAddress == ip);
        }

        public async Task<BlockedIP?> GetBlockedIPDetailsAsync(string ip)
        {
            return await _context.BlockedIPs.FirstOrDefaultAsync(b => b.IpAddress == ip);
        }
    }
}
