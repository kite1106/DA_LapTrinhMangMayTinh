using Microsoft.EntityFrameworkCore;
using SecurityMonitor.Data;
using SecurityMonitor.Models;
using SecurityMonitor.Services.Interfaces;

namespace SecurityMonitor.Services.Implementation
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
            // Kiểm tra IP đã bị block chưa
            var existingBlock = await _context.BlockedIPs
                .FirstOrDefaultAsync(b => b.IpAddress == ip);

            if (existingBlock != null)
            {
                // Cập nhật thông tin block nếu IP đã tồn tại
                existingBlock.Reason = reason;
                existingBlock.BlockedBy = blockedBy;
                existingBlock.BlockedAt = DateTime.UtcNow;
                _context.BlockedIPs.Update(existingBlock);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Cập nhật thông tin block cho IP {IP}", ip);
                return existingBlock;
            }

            // Tạo block mới
            var blockedIP = new BlockedIP(ip, reason, blockedBy);

            _context.BlockedIPs.Add(blockedIP);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Đã block IP {IP}", ip);
            return blockedIP;
        }

        public async Task<bool> UnblockIPAsync(string ip)
        {
            var blockedIP = await _context.BlockedIPs
                .FirstOrDefaultAsync(b => b.IpAddress == ip);

            if (blockedIP == null)
            {
                _logger.LogWarning("IP {IP} không tồn tại trong danh sách block", ip);
                return false;
            }

            _context.BlockedIPs.Remove(blockedIP);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Đã unblock IP {IP}", ip);
            return true;
        }

        public async Task<bool> IsIPBlockedAsync(string ip)
        {
            return await _context.BlockedIPs
                .AnyAsync(b => b.IpAddress == ip);
        }

        public async Task<BlockedIP?> GetBlockedIPDetailsAsync(string ip)
        {
            return await _context.BlockedIPs
                .FirstOrDefaultAsync(b => b.IpAddress == ip);
        }
    }
}
