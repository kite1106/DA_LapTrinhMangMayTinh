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

        public async Task<IEnumerable<Log>> GetAllLogsAsync()
        {
            return await _context.Logs
                .OrderByDescending(l => l.Timestamp)
                .ToListAsync();
        }

        public async Task<Log?> GetLogByIdAsync(long id)
        {
            return await _context.Logs.FindAsync(id);
        }

        public async Task<Log> CreateLogAsync(Log log)
        {
            log.Timestamp = DateTime.UtcNow;
            _context.Logs.Add(log);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Created new log entry with ID: {Id}", log.Id);
            return log;
        }

        public async Task<IEnumerable<Log>> GetLogsBySourceAsync(int sourceId)
        {
            return await _context.Logs
                .Where(l => l.LogSourceId == sourceId)
                .OrderByDescending(l => l.Timestamp)
                .ToListAsync();
        }

        public async Task<IEnumerable<Log>> GetLogsByDateRangeAsync(DateTime start, DateTime end)
        {
            return await _context.Logs
                .Where(l => l.Timestamp >= start && l.Timestamp <= end)
                .OrderByDescending(l => l.Timestamp)
                .ToListAsync();
        }

        public async Task<IEnumerable<Log>> GetRecentLogsAsync(TimeSpan duration)
        {
            var cutoffDate = DateTime.UtcNow.Subtract(duration);
            return await _context.Logs
                .Where(l => l.Timestamp >= cutoffDate)
                .OrderByDescending(l => l.Timestamp)
                .ToListAsync();
        }

        public async Task ProcessLogAsync(long logId)
        {
            var log = await _context.Logs.FindAsync(logId);
            if (log != null)
            {
                // Add any processing logic here
                _logger.LogInformation("Processed log with ID: {Id}", logId);
            }
        }
    }
}
