using Microsoft.EntityFrameworkCore;
using SecurityMonitor.Data;
using SecurityMonitor.Models;
using SecurityMonitor.Services.Interfaces;

namespace SecurityMonitor.Services.Implementation;

public class LogSourceService : ILogSourceService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<LogSourceService> _logger;

    public LogSourceService(ApplicationDbContext context, ILogger<LogSourceService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<LogSource?> GetLogSourceByNameAsync(string name)
    {
        return await _context.LogSources.FirstOrDefaultAsync(ls => ls.Name == name);
    }

    public async Task<LogSource> CreateLogSourceAsync(LogSource logSource)
    {
        logSource.LastSeenAt = DateTime.UtcNow;
        _context.LogSources.Add(logSource);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Created new log source: {Name}", logSource.Name);
        return logSource;
    }

    public async Task<IEnumerable<LogSource>> GetAllLogSourcesAsync()
    {
        return await _context.LogSources.ToListAsync();
    }

    public async Task<LogSource?> GetLogSourceByIdAsync(int id)
    {
        return await _context.LogSources.FindAsync(id);
    }

    public async Task UpdateLogSourceAsync(LogSource logSource)
    {
        logSource.LastSeenAt = DateTime.UtcNow;
        _context.LogSources.Update(logSource);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Updated log source: {Name}", logSource.Name);
    }
}
