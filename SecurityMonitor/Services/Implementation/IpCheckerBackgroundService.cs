using Microsoft.Extensions.DependencyInjection;
using SecurityMonitor.Services.Interfaces;
using SecurityMonitor.Models;
using System.Net.Http;

namespace SecurityMonitor.Services.Implementation
{
    public class IpCheckerBackgroundService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<IpCheckerBackgroundService> _logger;
        private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(15);

        public IpCheckerBackgroundService(
            IServiceScopeFactory scopeFactory,
            ILogger<IpCheckerBackgroundService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        private TimeSpan _blacklistCheckInterval = TimeSpan.FromHours(12); // Check blacklist twice a day
        private DateTime _lastBlacklistCheck = DateTime.MinValue;

        private List<string> PrioritizeIPs(IEnumerable<Log> logs)
        {
            // Group logs by IP and count occurrences
            var ipStats = logs
                .Where(l => !string.IsNullOrEmpty(l.IpAddress))
                .GroupBy(l => l.IpAddress!)
                .Select(g => new
                {
                    IP = g.Key,
                    Count = g.Count(),
                    ErrorCount = g.Count(l => l.EventType?.Contains("error", StringComparison.OrdinalIgnoreCase) ?? false),
                    LoginFailures = g.Count(l => (l.EventType?.Contains("login", StringComparison.OrdinalIgnoreCase) ?? false) &&
                                               (l.Message?.Contains("failed", StringComparison.OrdinalIgnoreCase) ?? false)),
                    SuspiciousUrls = g.Count(l => (l.Message?.Contains("admin", StringComparison.OrdinalIgnoreCase) ?? false) ||
                                                (l.Message?.Contains("wp-", StringComparison.OrdinalIgnoreCase) ?? false) ||
                                                (l.Message?.Contains("setup", StringComparison.OrdinalIgnoreCase) ?? false))
                })
                .OrderByDescending(x => x.ErrorCount * 3 + x.LoginFailures * 2 + x.SuspiciousUrls)
                .ThenByDescending(x => x.Count)
                .Select(x => x.IP)
                .Take(40)
                .ToList();

            // Randomize the order within the priority set to avoid always hitting the same IPs first
            Random rnd = new Random();
            return ipStats.OrderBy(x => rnd.Next()).ToList();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using (var scope = _scopeFactory.CreateScope())
                    {
                        var logService = scope.ServiceProvider.GetRequiredService<ILogService>();
                        var abuseIPDBService = scope.ServiceProvider.GetRequiredService<IAbuseIPDBService>();
                        var ipCheckCache = scope.ServiceProvider.GetRequiredService<IIpCheckCache>();

                        var logs = await logService.GetRecentLogsAsync(TimeSpan.FromHours(1));
                        var prioritizedIps = PrioritizeIPs(logs);

                        foreach (var ip in prioritizedIps)
                        {
                            try
                            {
                                if (!ipCheckCache.ShouldCheck(ip))
                                {
                                    _logger.LogDebug("Skipping previously checked IP: {Ip}", ip);
                                    continue;
                                }

                                await abuseIPDBService.CheckIPAsync(ip);
                                
                                // Random delay between 1-3 seconds between requests
                                var delay = Random.Shared.Next(1000, 3000);
                                await Task.Delay(delay, stoppingToken);
                            }
                            catch (HttpRequestException ex) when (ex.Message.Contains("429"))
                            {
                                _logger.LogWarning("Rate limit hit while checking IP: {Ip}", ip);
                                // If we hit the rate limit, wait longer before the next check
                                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                                break;
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error checking IP: {Ip}", ip);
                            }
                        }

                        // Check blacklist only twice per day
                        var now = DateTime.UtcNow;
                        if (now - _lastBlacklistCheck > _blacklistCheckInterval)
                        {
                            try
                            {
                                await abuseIPDBService.GetBlacklistedIPsAsync();
                                _lastBlacklistCheck = now;
                            }
                            catch (HttpRequestException ex) when (ex.Message.Contains("429"))
                            {
                                _logger.LogWarning("Rate limit hit while getting blacklist from AbuseIPDB.");
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Unexpected error in GetBlacklistedIPsAsync");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during IP checking background task");
                }

                // Random delay between 14-16 minutes before next check
                var nextInterval = TimeSpan.FromMinutes(Random.Shared.Next(14, 17));
                await Task.Delay(nextInterval, stoppingToken);
            }
        }
    }
}
