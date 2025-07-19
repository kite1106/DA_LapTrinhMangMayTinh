using SecurityMonitor.Models;

namespace SecurityMonitor.Services.Interfaces
{
    public interface IAuditService
    {
        Task LogActivityAsync(string userId, string action, string entityType, string entityId, string? details = null, string? ipAddress = null);
        Task<IEnumerable<AuditLog>> GetUserActivityAsync(string userId);
        Task<IEnumerable<AuditLog>> GetActivityByDateRangeAsync(DateTime start, DateTime end);
    }
}
