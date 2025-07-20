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
        private readonly TimeSpan _checkInterval = TimeSpan.FromSeconds(1); // Kiểm tra mỗi 1 giây
        private readonly TimeSpan _blacklistCheckInterval = TimeSpan.FromHours(12); // Kiểm tra blacklist mỗi 12 giờ
        private readonly SemaphoreSlim _requestSemaphore = new SemaphoreSlim(1, 1);
        private readonly Dictionary<string, DateTime> _lastCheckTimes = new();
        private int _dailyRequestCount;
        private DateTime _requestCountResetTime;
        private DateTime _lastBlacklistCheck;

        public IpCheckerBackgroundService(
            IServiceScopeFactory scopeFactory,
            ILogger<IpCheckerBackgroundService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _dailyRequestCount = 0;
            _requestCountResetTime = DateTime.UtcNow;
            _lastBlacklistCheck = DateTime.MinValue;
        }

        private IEnumerable<string> GetSuspiciousIPs(IEnumerable<Log> logs, int maxIPs = 10)
        {
            var ipStats = logs
                .Where(l => !string.IsNullOrEmpty(l.IpAddress))
                .GroupBy(l => l.IpAddress!)
                .Where(g => g.Count() >= 2)
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
                .Where(x => x.ErrorCount > 0 || x.LoginFailures > 0 || x.SuspiciousUrls > 0)
                .OrderByDescending(x => x.ErrorCount * 3 + x.LoginFailures * 2 + x.SuspiciousUrls)
                .ThenByDescending(x => x.Count)
                .Take(maxIPs)
                .Select(x => x.IP)
                .ToList();

            return ipStats;
        }

        private void ResetDailyCounterIfNeeded()
        {
            var now = DateTime.UtcNow;
            if ((now - _requestCountResetTime).TotalHours >= 24)
            {
                _dailyRequestCount = 0;
                _requestCountResetTime = now;
                _logger.LogInformation("Reset daily API counter");
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("IP Checker service starting...");
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); // Đợi để các service khác khởi động

            while (!stoppingToken.IsCancellationRequested)
            {
                if (!await _requestSemaphore.WaitAsync(0))
                {
                    _logger.LogInformation("Another check is in progress, waiting...");
                    await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                    continue;
                }

                try
                {
                    ResetDailyCounterIfNeeded();

                    if (_dailyRequestCount >= 950)
                    {
                        _logger.LogWarning("Daily API limit reached. Waiting until reset.");
                        await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
                        continue;
                    }

                    await Task.Delay(_checkInterval, stoppingToken);

                    using (var scope = _scopeFactory.CreateScope())
                    {
                        var logService = scope.ServiceProvider.GetRequiredService<ILogService>();
                        var abuseIPDBService = scope.ServiceProvider.GetRequiredService<IAbuseIPDBService>();
                        var ipCheckCache = scope.ServiceProvider.GetRequiredService<IIpCheckCache>();

                        var logs = await logService.GetRecentLogsAsync(TimeSpan.FromMinutes(30));
                        _logger.LogInformation("Retrieved {count} logs for analysis", logs.Count());

                        var suspiciousIPs = GetSuspiciousIPs(logs, 10).ToList();
                        if (!suspiciousIPs.Any())
                        {
                            _logger.LogInformation("No suspicious IPs found in logs");
                            await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
                            continue;
                        }

                        var currentTime = DateTime.UtcNow;
                        var checkedCount = 0;

                        foreach (var ip in suspiciousIPs)
                        {
                            if (_dailyRequestCount >= 950)
                            {
                                _logger.LogWarning("Daily API limit reached. Stopping IP checks for now.");
                                break;
                            }

                            if (!ipCheckCache.ShouldCheck(ip))
                            {
                                _logger.LogDebug("Skipping previously checked IP: {Ip}", ip);
                                continue;
                            }

                            if (_lastCheckTimes.TryGetValue(ip, out var lastCheck) &&
                                (currentTime - lastCheck).TotalSeconds < 3)
                            {
                                _logger.LogDebug("Skipping IP {Ip} - checked recently", ip);
                                continue;
                            }

                            try
                            {
                                await abuseIPDBService.CheckIPAsync(ip);
                                _dailyRequestCount++;
                                _lastCheckTimes[ip] = currentTime;
                                checkedCount++;
                                _logger.LogInformation("Checked suspicious IP: {Ip} (Daily count: {Count})", ip, _dailyRequestCount);
                            }
                            catch (HttpRequestException ex) when (ex.Message.Contains("429"))
                            {
                                _logger.LogWarning("Rate limit hit while checking IP: {Ip}", ip);
                                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
                                break;
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error checking IP: {Ip}", ip);
                            }
                        }

                        _logger.LogInformation("Checked {count} suspicious IPs in this batch", checkedCount);

                        var now = DateTime.UtcNow;
                        if (now - _lastBlacklistCheck > _blacklistCheckInterval && _dailyRequestCount < 900)
                        {
                            try
                            {
                                await abuseIPDBService.GetBlacklistedIPsAsync();
                                _dailyRequestCount++;
                                _lastBlacklistCheck = now;
                                _logger.LogInformation("Updated blacklist (Daily count: {Count})", _dailyRequestCount);
                            }
                            catch (HttpRequestException ex) when (ex.Message.Contains("429"))
                            {
                                _logger.LogWarning("Rate limit hit while getting blacklist from AbuseIPDB");
                                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error getting blacklist");
                            }
                        }

                        await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken); // Delay giữa các batch
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during IP checking background task");
                }
                finally
                {
                    _requestSemaphore.Release();
                }

                await Task.Delay(_checkInterval, stoppingToken);
            }
        }
    }
}
