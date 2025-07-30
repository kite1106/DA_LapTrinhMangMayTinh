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
                .OrderByDescending(l => l.Timestamp)
                .ToListAsync();
        }

        public async Task<LogEntry?> GetLogByIdAsync(long id)
        {
            return await _context.LogEntries.FindAsync(id);
        }

        public async Task<LogEntry> CreateLogAsync(LogEntry log)
        {
            log.Timestamp = DateTime.UtcNow;
            
            // Tự động set LogLevelTypeId mặc định nếu không có
            if (log.LogLevelTypeId == 0)
            {
                var defaultLevel = await _context.LogLevelTypes.FirstOrDefaultAsync(lt => lt.Name == "Information");
                if (defaultLevel != null)
                {
                    log.LogLevelTypeId = defaultLevel.Id;
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
                    log.LogLevelTypeId = newLevel.Id;
                }
            }
            
            _context.LogEntries.Add(log);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Created new log entry with ID: {Id}", log.Id);
            return log;
        }

        public async Task<IEnumerable<LogEntry>> GetLogsBySourceAsync(int sourceId)
        {
            return await _context.LogEntries
                .Where(l => l.LogSourceId == sourceId)
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
                .FirstOrDefaultAsync(ls => ls.Name == name);
        }

        public async Task<LogSource> CreateLogSourceAsync(LogSource source)
        {
            _context.LogSources.Add(source);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Created new log source: {Name}", source.Name);
            return source;
        }

        public async Task UpdateLogSourceAsync(LogSource source)
        {
            _context.LogSources.Update(source);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Updated log source: {Name}", source.Name);
        }
    }
}
