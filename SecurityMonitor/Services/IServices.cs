using SecurityMonitor.Models;

namespace SecurityMonitor.Services;

public interface IAlertService
{
    Task<IEnumerable<Alert>> GetAllAlertsAsync();
    Task<Alert?> GetAlertByIdAsync(int id);
    Task<Alert> CreateAlertAsync(Alert alert);
    Task<Alert?> UpdateAlertAsync(int id, Alert alert);
    Task<bool> DeleteAlertAsync(int id);
    Task<IEnumerable<Alert>> GetAlertsBySeverityAsync(int severityLevelId);
    Task<IEnumerable<Alert>> GetAlertsByStatusAsync(int statusId);
    Task<Alert?> AssignAlertAsync(int alertId, string userId);
    Task<Alert?> ResolveAlertAsync(int alertId, string userId, string resolution);
}

public interface ILogService
{
    Task<IEnumerable<Log>> GetAllLogsAsync();
    Task<Log?> GetLogByIdAsync(long id);
    Task<Log> CreateLogAsync(Log log);
    Task<IEnumerable<Log>> GetLogsBySourceAsync(int sourceId);
    Task<IEnumerable<Log>> GetLogsByDateRangeAsync(DateTime start, DateTime end);
    Task ProcessLogAsync(long logId);
}

public interface IAuditService
{
    Task LogActivityAsync(string userId, string action, string entityType, string entityId, string? details = null, string? ipAddress = null);
    Task<IEnumerable<AuditLog>> GetUserActivityAsync(string userId);
    Task<IEnumerable<AuditLog>> GetActivityByDateRangeAsync(DateTime start, DateTime end);
}
