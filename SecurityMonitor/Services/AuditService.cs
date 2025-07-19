using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SecurityMonitor.Data;
using SecurityMonitor.Models;
using SecurityMonitor.Services.Interfaces;

namespace SecurityMonitor.Services;

public class AuditService : SecurityMonitor.Services.Interfaces.IAuditService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AuditService> _logger;

    public AuditService(
        ApplicationDbContext context,
        ILogger<AuditService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task LogActivityAsync(
        string userId,
        string action,
        string entityType,
        string entityId,
        string? details = null,
        string? ipAddress = null)
    {
        // Chỉ lưu UserId khi có user thực sự
        var user = !string.IsNullOrEmpty(userId) ? await _context.Users.FindAsync(userId) : null;

        var auditLog = new AuditLog
        {
            UserId = user?.Id,
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
            "Audit log created: {Action} on {EntityType} {EntityId} by {UserId}",
            action, entityType, entityId, userId);
    }

    public async Task<IEnumerable<AuditLog>> GetUserActivityAsync(string userId)
    {
        return await _context.AuditLogs
            .Where(a => a.UserId == userId)
            .Include(a => a.User)
            .OrderByDescending(a => a.Timestamp)
            .ToListAsync();
    }

    public async Task<IEnumerable<AuditLog>> GetActivityByDateRangeAsync(DateTime start, DateTime end)
    {
        return await _context.AuditLogs
            .Where(a => a.Timestamp >= start && a.Timestamp <= end)
            .Include(a => a.User)
            .OrderByDescending(a => a.Timestamp)
            .ToListAsync();
    }
}
