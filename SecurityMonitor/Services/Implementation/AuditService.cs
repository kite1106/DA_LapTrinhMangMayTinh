using SecurityMonitor.Models;
using SecurityMonitor.Data;
using SecurityMonitor.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace SecurityMonitor.Services.Implementation
{
    public class AuditService : IAuditService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AuditService> _logger;

        public AuditService(ApplicationDbContext context, ILogger<AuditService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task LogActivityAsync(
            string userId,
            string action,
            string entityType,
            string entityId,
            string? details,
            string? ipAddress = null)
        {
            var auditLog = new AuditLog
            {
                UserId = userId,
                Action = action,
                EntityType = entityType,
                EntityId = entityId,
                Details = details,
                IpAddress = ipAddress,
                Timestamp = DateTime.UtcNow
            };

            _context.AuditLogs.Add(auditLog);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Audit log created - User: {UserId}, Action: {Action}, EntityType: {EntityType}, EntityId: {EntityId}",
                userId, action, entityType, entityId);
        }

        public async Task<IEnumerable<AuditLog>> GetUserActivityAsync(string userId)
        {
            return await _context.AuditLogs
                .Where(log => log.UserId == userId)
                .OrderByDescending(log => log.Timestamp)
                .ToListAsync();
        }

        public async Task<IEnumerable<AuditLog>> GetRecentActivityAsync(int count = 100)
        {
            return await _context.AuditLogs
                .OrderByDescending(log => log.Timestamp)
                .Take(count)
                .ToListAsync();
        }

        public async Task<IEnumerable<AuditLog>> GetActivityByDateRangeAsync(DateTime startDate, DateTime endDate)
        {
            return await _context.AuditLogs
                .Where(log => log.Timestamp >= startDate && log.Timestamp <= endDate)
                .OrderByDescending(log => log.Timestamp)
                .ToListAsync();
        }
    }
}
