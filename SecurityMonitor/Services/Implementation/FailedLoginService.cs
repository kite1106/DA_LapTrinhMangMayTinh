using SecurityMonitor.Services.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using SecurityMonitor.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace SecurityMonitor.Services.Implementation;

public class FailedLoginService : IFailedLoginService
{
    private readonly IMemoryCache _cache;
    private readonly IHubContext<AlertHub> _alertHub;
    private readonly TimeSpan _expirationTime = TimeSpan.FromHours(1);

    public FailedLoginService(IMemoryCache cache, IHubContext<AlertHub> alertHub)
    {
        _cache = cache;
        _alertHub = alertHub;
    }

    public async Task<int> GetFailedAttemptsAsync(string ipAddress)
    {
        return _cache.GetOrCreate($"failed_login_{ipAddress}", entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = _expirationTime;
            return 0;
        });
    }

    public async Task AddFailedAttemptAsync(string ipAddress, string username)
    {
        var attempts = await GetFailedAttemptsAsync(ipAddress) + 1;
        _cache.Set($"failed_login_{ipAddress}", attempts, _expirationTime);

        // Gửi cảnh báo nếu số lần thất bại vượt ngưỡng
        if (attempts >= 3)
        {
            string severity = attempts >= 5 ? "High" : "Medium";
            await _alertHub.Clients.All.SendAsync("ReceiveLoginAlert", new
            {
                title = "Cảnh báo đăng nhập thất bại",
                description = $"Phát hiện {attempts} lần đăng nhập thất bại từ địa chỉ IP: {ipAddress}",
                email = username,
                ip = ipAddress,
                failedAttempts = attempts,
                severity = severity,
                timestamp = DateTime.Now
            });
        }
    }

    public async Task ResetFailedAttemptsAsync(string ipAddress)
    {
        _cache.Remove($"failed_login_{ipAddress}");
    }
}
