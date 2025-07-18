using Microsoft.EntityFrameworkCore;
using SecurityMonitor.Data;
using SecurityMonitor.Models;

namespace SecurityMonitor.Services;

public class LogService : ILogService
{
    private readonly ApplicationDbContext _context;
    private readonly IAuditService _auditService;
    private readonly ILogger<LogService> _logger;

    public LogService(
        ApplicationDbContext context,
        IAuditService auditService,
        ILogger<LogService> logger)
    {
        _context = context;
        _auditService = auditService;
        _logger = logger;
    }

    public async Task<IEnumerable<Log>> GetAllLogsAsync()
    {
        return await _context.Logs
            .Include(l => l.LogSource)
            .OrderByDescending(l => l.Timestamp)
            .ToListAsync();
    }

    public async Task<Log?> GetLogByIdAsync(long id)
    {
        return await _context.Logs
            .Include(l => l.LogSource)
            .FirstOrDefaultAsync(l => l.Id == id);
    }

    public async Task<Log> CreateLogAsync(Log log)
    {
        log.Timestamp = DateTime.Now;
        _context.Logs.Add(log);
        await _context.SaveChangesAsync();

        await _auditService.LogActivityAsync(
            "System",
            "Create",
            "Log",
            log.Id.ToString(),
            $"Created log from source: {log.LogSourceId}"
        );

        return log;
    }

    public async Task<IEnumerable<Log>> GetLogsBySourceAsync(int sourceId)
    {
        return await _context.Logs
            .Where(l => l.LogSourceId == sourceId)
            .Include(l => l.LogSource)
            .OrderByDescending(l => l.Timestamp)
            .ToListAsync();
    }

    public async Task<IEnumerable<Log>> GetLogsByDateRangeAsync(DateTime start, DateTime end)
    {
        return await _context.Logs
            .Where(l => l.Timestamp >= start && l.Timestamp <= end)
            .Include(l => l.LogSource)
            .OrderByDescending(l => l.Timestamp)
            .ToListAsync();
    }

    public async Task ProcessLogAsync(long logId)
    {
        var log = await _context.Logs.FindAsync(logId);
        if (log == null) return;

        log.ProcessedAt = DateTime.Now;
        await _context.SaveChangesAsync();

        await _auditService.LogActivityAsync(
            "System",
            "Process",
            "Log",
            logId.ToString(),
            $"Processed log from source: {log.LogSourceId}"
        );
    }
}
