using SecurityMonitor.Models;
using SecurityMonitor.Data;
using SecurityMonitor.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace SecurityMonitor.Services.Implementation
{
    public class LogService : ILogService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<LogService> _logger;

        public LogService(ApplicationDbContext context, ILogger<LogService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<IEnumerable<LogEntry>> GetAllLogsAsync()
        {
            return await _context.LogEntries
                .Include(l => l.LogLevelType)
                .Include(l => l.LogSource)
                .OrderByDescending(l => l.Timestamp)
                .ToListAsync();
        }

        public async Task<LogEntry?> GetLogByIdAsync(long id)
        {
            return await _context.LogEntries
                .Include(l => l.LogLevelType)
                .Include(l => l.LogSource)
                .FirstOrDefaultAsync(l => l.Id == id);
        }

        public async Task<LogEntry> CreateLogAsync(LogEntry logEntry)
        {
            logEntry.Timestamp = DateTime.UtcNow;
            
            // Tự động set LogLevelTypeId mặc định nếu không có
            if (logEntry.LogLevelTypeId == 0)
            {
                var defaultLevel = await _context.LogLevelTypes.FirstOrDefaultAsync(lt => lt.Name == "Information");
                if (defaultLevel != null)
                {
                    logEntry.LogLevelTypeId = defaultLevel.Id;
                }
                else
                {
                    // Nếu không có LogLevelType nào, tạo một cái mặc định
                    var newLevel = new LogLevelType 
                    { 
                        Name = "Information", 
                        Description = "Thông tin", 
                        Priority = 1 
                    };
                    _context.LogLevelTypes.Add(newLevel);
                    await _context.SaveChangesAsync();
                    logEntry.LogLevelTypeId = newLevel.Id;
                }
            }
            
            _context.LogEntries.Add(logEntry);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Created log entry: {Id}", logEntry.Id);
            return logEntry;
        }

        public async Task UpdateLogAsync(LogEntry logEntry)
        {
            _context.LogEntries.Update(logEntry);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Updated log entry: {Id}", logEntry.Id);
        }

        public async Task DeleteLogAsync(long id)
        {
            var logEntry = await _context.LogEntries.FindAsync(id);
            if (logEntry != null)
            {
                _context.LogEntries.Remove(logEntry);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Deleted log entry: {Id}", id);
            }
        }

        public async Task<IEnumerable<LogEntry>> GetLogsBySourceAsync(int sourceId)
        {
            return await _context.LogEntries
                .Where(l => l.LogSourceId == sourceId)
                .Include(l => l.LogLevelType)
                .Include(l => l.LogSource)
                .OrderByDescending(l => l.Timestamp)
                .ToListAsync();
        }

        public async Task<IEnumerable<LogEntry>> GetLogsByDateRangeAsync(DateTime start, DateTime end)
        {
            return await _context.LogEntries
                .Where(l => l.Timestamp >= start && l.Timestamp <= end)
                .OrderByDescending(l => l.Timestamp)
                .ToListAsync();
        }

        public async Task<IEnumerable<LogEntry>> GetRecentLogsAsync(TimeSpan duration)
        {
            var cutoffDate = DateTime.UtcNow.Subtract(duration);
            return await _context.LogEntries
                .Where(l => l.Timestamp >= cutoffDate)
                .OrderByDescending(l => l.Timestamp)
                .ToListAsync();
        }

        public async Task ProcessLogAsync(long logId)
        {
            var log = await _context.LogEntries.FindAsync(logId);
            if (log != null)
            {
                // Add any processing logic here
                _logger.LogInformation("Processed log with ID: {Id}", logId);
            }
        }

        public async Task<LogSource?> GetLogSourceByNameAsync(string name)
        {
            return await _context.LogSources
                .FirstOrDefaultAsync(s => s.Name == name);
        }

        public async Task<LogSource> CreateLogSourceAsync(LogSource source)
        {
            source.LastSeenAt = DateTime.UtcNow;
            _context.LogSources.Add(source);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Created log source: {Name}", source.Name);
            return source;
        }

        public async Task UpdateLogSourceAsync(LogSource source)
        {
            source.LastSeenAt = DateTime.UtcNow;
            _context.LogSources.Update(source);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Updated log source: {Name}", source.Name);
        }

        public async Task<IEnumerable<LogEntry>> GetLogsByTimeRangeAsync(DateTime startTime, DateTime endTime)
        {
            return await _context.LogEntries
                .Where(l => l.Timestamp >= startTime && l.Timestamp <= endTime)
                .Include(l => l.LogLevelType)
                .Include(l => l.LogSource)
                .OrderByDescending(l => l.Timestamp)
                .ToListAsync();
        }

        public async Task<IEnumerable<LogEntry>> GetLogsByLevelAsync(int levelId)
        {
            return await _context.LogEntries
                .Where(l => l.LogLevelTypeId == levelId)
                .Include(l => l.LogLevelType)
                .Include(l => l.LogSource)
                .OrderByDescending(l => l.Timestamp)
                .ToListAsync();
        }

        public async Task<int> GetLogCountAsync()
        {
            return await _context.LogEntries.CountAsync();
        }

        public async Task<Dictionary<string, int>> GetLogStatisticsAsync(TimeSpan window)
        {
            var cutoffTime = DateTime.UtcNow.Subtract(window);
            var logs = await _context.LogEntries
                .Where(l => l.Timestamp >= cutoffTime)
                .Include(l => l.LogLevelType)
                .Include(l => l.LogSource)
                .ToListAsync();

            return new Dictionary<string, int>
            {
                ["Total"] = logs.Count,
                ["Info"] = logs.Count(l => l.LogLevelType?.Name == "Info"),
                ["Warning"] = logs.Count(l => l.LogLevelType?.Name == "Warning"),
                ["Error"] = logs.Count(l => l.LogLevelType?.Name == "Error"),
                ["Critical"] = logs.Count(l => l.LogLevelType?.Name == "Critical"),
                ["Success"] = logs.Count(l => l.WasSuccessful),
                ["Failed"] = logs.Count(l => !l.WasSuccessful)
            };
        }
    }
}
