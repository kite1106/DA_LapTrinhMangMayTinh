using SecurityMonitor.Models;

namespace SecurityMonitor.Services.Interfaces
{
    public interface ILogService
    {
        Task<IEnumerable<LogEntry>> GetAllLogsAsync();
        Task<LogEntry?> GetLogByIdAsync(long id);
        Task<LogEntry> CreateLogAsync(LogEntry log);
        Task<IEnumerable<LogEntry>> GetLogsBySourceAsync(int sourceId);
        Task<IEnumerable<LogEntry>> GetLogsByDateRangeAsync(DateTime start, DateTime end);
        Task<IEnumerable<LogEntry>> GetRecentLogsAsync(TimeSpan duration);
        Task ProcessLogAsync(long logId);
        
        // Log Source methods
        Task<LogSource?> GetLogSourceByNameAsync(string name);
        Task<LogSource> CreateLogSourceAsync(LogSource source);
        Task UpdateLogSourceAsync(LogSource source);
    }
}
