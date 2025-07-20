using SecurityMonitor.Models;

namespace SecurityMonitor.Services.Interfaces
{
    public interface ILogService
    {
        Task<IEnumerable<Log>> GetAllLogsAsync();
        Task<Log?> GetLogByIdAsync(long id);
        Task<Log> CreateLogAsync(Log log);
        Task<IEnumerable<Log>> GetLogsBySourceAsync(int sourceId);
        Task<IEnumerable<Log>> GetLogsByDateRangeAsync(DateTime start, DateTime end);
        Task<IEnumerable<Log>> GetRecentLogsAsync(TimeSpan duration);
        Task ProcessLogAsync(long logId);
    }
}
