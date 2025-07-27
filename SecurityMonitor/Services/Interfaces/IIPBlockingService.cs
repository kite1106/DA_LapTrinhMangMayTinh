using SecurityMonitor.Models;

namespace SecurityMonitor.Services.Interfaces
{
    public interface IIPBlockingService
    {
        Task<IEnumerable<BlockedIP>> GetBlockedIPsAsync();
        Task<BlockedIP> BlockIPAsync(string ip, string reason, string blockedBy);
        Task<bool> UnblockIPAsync(string ip);
        Task<bool> IsIPBlockedAsync(string ip);
        Task<BlockedIP?> GetBlockedIPDetailsAsync(string ip);
    }
}
