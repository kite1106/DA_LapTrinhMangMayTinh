using SecurityMonitor.Models;

namespace SecurityMonitor.Services.Interfaces
{
    public interface IIPCheckerService
    {
        Task<bool> IsBlockedAsync(string ipAddress);
        Task<IEnumerable<Alert>> CheckIPAsync(string ipAddress);
        Task<IEnumerable<string>> GetBlockedIPsAsync();
        Task BlockIPAsync(string ipAddress);
        Task UnblockIPAsync(string ipAddress);
    }
}
