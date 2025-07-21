using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.SignalR;
using SecurityMonitor.Hubs;
using SecurityMonitor.Models;
using SecurityMonitor.Services.Interfaces;

namespace SecurityMonitor.Services.Implementation
{
    public class IpCheckerBackgroundService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<IpCheckerBackgroundService> _logger;
        private readonly IHubContext<AlertHub> _hubContext;
        private readonly TimeSpan _checkInterval = TimeSpan.FromSeconds(15);
        private readonly SemaphoreSlim _requestSemaphore = new(1, 1);
        private readonly Dictionary<string, DateTime> _lastCheckTimes = new();
        private int _dailyRequestCount;
        private DateTime _requestCountResetTime;

        public IpCheckerBackgroundService(
            IServiceScopeFactory scopeFactory,
            ILogger<IpCheckerBackgroundService> logger,
            IHubContext<AlertHub> hubContext)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _hubContext = hubContext;
            _dailyRequestCount = 0;
            _requestCountResetTime = DateTime.UtcNow;
        }

        private IEnumerable<string> GetSuspiciousIPs(IEnumerable<Log> logs, int maxIPs = 5)
        {
            return logs
                .Where(l => !string.IsNullOrEmpty(l.IpAddress))
                .GroupBy(l => l.IpAddress!)
                .Select(g => new
                {
                    IP = g.Key,
                    Count = g.Count(),
                    ErrorCount = g.Count(l => l.EventType?.Contains("error", StringComparison.OrdinalIgnoreCase) ?? false),
                    LoginFailures = g.Count(l => (l.EventType?.Contains("login", StringComparison.OrdinalIgnoreCase) ?? false)
                        && (l.Message?.Contains("failed", StringComparison.OrdinalIgnoreCase) ?? false)),
                    SuspiciousUrls = g.Count(l => (l.Message?.Contains("admin", StringComparison.OrdinalIgnoreCase) ?? false)
                        || (l.Message?.Contains("wp-", StringComparison.OrdinalIgnoreCase) ?? false)
                        || (l.Message?.Contains("setup", StringComparison.OrdinalIgnoreCase) ?? false))
                })
                .Where(x => x.ErrorCount > 0 || x.LoginFailures > 0 || x.SuspiciousUrls > 0)
                .OrderByDescending(x => x.ErrorCount * 3 + x.LoginFailures * 2 + x.SuspiciousUrls)
                .ThenByDescending(x => x.Count)
                .Take(maxIPs)
                .Select(x => x.IP)
                .ToList();
        }

        private void ResetDailyCounterIfNeeded()
        {
            if ((DateTime.UtcNow - _requestCountResetTime).TotalHours >= 24)
            {
                _dailyRequestCount = 0;
                _requestCountResetTime = DateTime.UtcNow;
                _logger.LogInformation("Reset daily API counter");
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("IP Checker service starting...");
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                if (!await _requestSemaphore.WaitAsync(0))
                {
                    await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                    continue;
                }

                try
                {
                    ResetDailyCounterIfNeeded();
                    if (_dailyRequestCount >= 950)
                    {
                        await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
                        continue;
                    }

                    using var scope = _scopeFactory.CreateScope();
                    var logService = scope.ServiceProvider.GetRequiredService<ILogService>();
                    var abuseIPDBService = scope.ServiceProvider.GetRequiredService<IAbuseIPDBService>();
                    var ipCheckCache = scope.ServiceProvider.GetRequiredService<IIpCheckCache>();
                    var alertService = scope.ServiceProvider.GetRequiredService<IAlertService>();

                    var logs = await logService.GetRecentLogsAsync(TimeSpan.FromMinutes(30));
                    var suspiciousIPs = GetSuspiciousIPs(logs).ToList();
                    if (!suspiciousIPs.Any())
                    {
                        await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
                        continue;
                    }

                    var currentTime = DateTime.UtcNow;
                    foreach (var ip in suspiciousIPs)
                    {
                        if (_dailyRequestCount >= 950) break;
                        if (await alertService.GetRecentAlertByIpAsync(ip, TimeSpan.FromMinutes(30))) continue;
                        if (!ipCheckCache.ShouldCheck(ip)) continue;
                        if (_lastCheckTimes.TryGetValue(ip, out var lastCheck) &&
                            (currentTime - lastCheck).TotalSeconds < 10) continue;

                        try
                        {
                            var alerts = await abuseIPDBService.CheckIPAsync(ip);
                            foreach (var alert in alerts)
                            {
                                await alertService.CreateAlertAsync(alert);
                                await _hubContext.Clients.All.SendAsync("ReceiveAlert", alert);
                            }
                            _dailyRequestCount++;
                            _lastCheckTimes[ip] = currentTime;
                        }
                        catch (HttpRequestException ex) when (ex.Message.Contains("429")) { break; }
                        catch { }
                    }

                    await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
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
