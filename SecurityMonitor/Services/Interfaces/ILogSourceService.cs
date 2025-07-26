using SecurityMonitor.Models;

namespace SecurityMonitor.Services.Interfaces;

public interface ILogSourceService
{
    Task<LogSource?> GetLogSourceByNameAsync(string name);
    Task<LogSource> CreateLogSourceAsync(LogSource logSource);
    Task<IEnumerable<LogSource>> GetAllLogSourcesAsync();
    Task<LogSource?> GetLogSourceByIdAsync(int id);
    Task UpdateLogSourceAsync(LogSource logSource);
}
