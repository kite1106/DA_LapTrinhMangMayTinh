using Microsoft.Extensions.Caching.Memory;

namespace SecurityMonitor.Services.Implementation
{
    public interface IIpCheckCache
    {
        bool ShouldCheck(string ip);
        void MarkChecked(string ip);
        void Clear();
    }

    public class IpCheckCache : IIpCheckCache
    {
        private readonly MemoryCache _cache;
        private readonly ILogger<IpCheckCache> _logger;

        public IpCheckCache(ILogger<IpCheckCache> logger)
        {
            _cache = new MemoryCache(new MemoryCacheOptions());
            _logger = logger;
        }

        public bool ShouldCheck(string ip)
        {
            if (string.IsNullOrEmpty(ip))
                return false;

            return !_cache.TryGetValue(ip, out _);
        }

        public void MarkChecked(string ip)
        {
            if (string.IsNullOrEmpty(ip))
                return;

            _cache.Set(ip, true, TimeSpan.FromHours(24));
            _logger.LogDebug("IP {IP} marked as checked for next 24 hours", ip);
        }

        public void Clear()
        {
            _cache.Clear();
            _logger.LogInformation("IP check cache cleared");
        }
    }
}
