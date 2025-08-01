using SecurityMonitor.Services.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace SecurityMonitor.Services.Implementation
{
    public class LogGenerationControlService : ILogGenerationControlService
    {
        private readonly IMemoryCache _cache;
        private readonly ILogger<LogGenerationControlService> _logger;
        private const string CACHE_KEY = "LogGenerationEnabled";

        public LogGenerationControlService(IMemoryCache cache, ILogger<LogGenerationControlService> logger)
        {
            _cache = cache;
            _logger = logger;
        }

        public bool IsLogGenerationEnabled => _cache.Get<bool?>(CACHE_KEY) ?? true; // Mặc định là bật

        public async Task<bool> EnableLogGenerationAsync()
        {
            try
            {
                _cache.Set(CACHE_KEY, true, TimeSpan.FromHours(24)); // Cache trong 24 giờ
                _logger.LogInformation("✅ Log generation enabled");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error enabling log generation");
                return false;
            }
        }

        public async Task<bool> DisableLogGenerationAsync()
        {
            try
            {
                _cache.Set(CACHE_KEY, false, TimeSpan.FromHours(24)); // Cache trong 24 giờ
                _logger.LogInformation("⏸️ Log generation disabled");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error disabling log generation");
                return false;
            }
        }

        public async Task<bool> ToggleLogGenerationAsync()
        {
            var currentStatus = IsLogGenerationEnabled;
            if (currentStatus)
            {
                return await DisableLogGenerationAsync();
            }
            else
            {
                return await EnableLogGenerationAsync();
            }
        }

        public async Task<bool> GetLogGenerationStatusAsync()
        {
            return IsLogGenerationEnabled;
        }
    }
} 