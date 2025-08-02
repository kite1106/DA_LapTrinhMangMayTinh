using SecurityMonitor.Models;

namespace SecurityMonitor.Services.Interfaces
{
    public interface ILogService
    {
        Task<LogEntry> CreateLogAsync(LogEntry logEntry);
        Task<LogEntry?> GetLogByIdAsync(long id);
        Task UpdateLogAsync(LogEntry logEntry);
        Task DeleteLogAsync(long id);
        Task<IEnumerable<LogEntry>> GetAllLogsAsync();
        Task<IEnumerable<LogEntry>> GetLogsByTimeRangeAsync(DateTime startTime, DateTime endTime);
        Task<IEnumerable<LogEntry>> GetLogsByLevelAsync(int levelId);
        Task<IEnumerable<LogEntry>> GetLogsBySourceAsync(int sourceId);
        Task<IEnumerable<LogEntry>> GetRecentLogsAsync(TimeSpan duration);
        Task<int> GetLogCountAsync();
        Task<Dictionary<string, int>> GetLogStatisticsAsync(TimeSpan window);
        
        // Log Source methods
        Task<LogSource?> GetLogSourceByNameAsync(string name);
        Task<LogSource> CreateLogSourceAsync(LogSource source);
        Task UpdateLogSourceAsync(LogSource source);
    }
}
