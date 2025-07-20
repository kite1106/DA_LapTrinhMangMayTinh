using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using SecurityMonitor.Models;
using SecurityMonitor.Hubs;
using SecurityMonitor.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace SecurityMonitor.Services.Implementation
{
    public class AbuseIPDBService : IAbuseIPDBService
    {
        private readonly AbuseIPDBConfig _config;
        private readonly IHttpClientFactory _clientFactory;
        private readonly IHubContext<AlertHub> _hubContext;
        private readonly IAlertService _alertService;
        private readonly ILogger<AbuseIPDBService> _logger;
        private readonly IIpCheckCache _ipCheckCache;
        private readonly SemaphoreSlim _rateLimitSemaphore;
        private int _hourlyCheckCount;
        private DateTime _lastResetTime;

        public AbuseIPDBService(
            IOptions<AbuseIPDBConfig> config,
            IHttpClientFactory clientFactory,
            IHubContext<AlertHub> hubContext,
            IAlertService alertService,
            ILogger<AbuseIPDBService> logger,
            IIpCheckCache ipCheckCache)
        {
            _config = config.Value;
            _clientFactory = clientFactory;
            _hubContext = hubContext;
            _alertService = alertService;
            _logger = logger;
            _ipCheckCache = ipCheckCache;
            _rateLimitSemaphore = new SemaphoreSlim(1, 1);
            _hourlyCheckCount = 0;
            _lastResetTime = DateTime.UtcNow;
        }

        private async Task ResetHourlyCounterIfNeeded()
        {
            var now = DateTime.UtcNow;
            if ((now - _lastResetTime).TotalHours >= 1)
            {
                _hourlyCheckCount = 0;
                _lastResetTime = now;
                _logger.LogInformation("Reset hourly API counter");
            }
        }

        private const int MAX_HOURLY_CHECKS = 40;

        public async Task<IEnumerable<Alert>> CheckIPAsync(string ipAddress)
        {
            try
            {
                if (!_ipCheckCache.ShouldCheck(ipAddress))
                {
                    _logger.LogDebug("Skipping IP check for {IP} - recently checked", ipAddress);
                    return Enumerable.Empty<Alert>();
                }

                await _rateLimitSemaphore.WaitAsync();
                try
                {
                    await ResetHourlyCounterIfNeeded();

                    if (_hourlyCheckCount >= MAX_HOURLY_CHECKS)
                    {
                        _logger.LogWarning("Hourly API limit reached, skipping check for IP {IP}", ipAddress);
                        return Enumerable.Empty<Alert>();
                    }

                    _hourlyCheckCount++;
                    var client = CreateClient();
                    var response = await client.GetAsync($"/api/v2/check?ipAddress={ipAddress}&maxAgeInDays={_config.MaxAgeInDays}");

                    if (response.StatusCode == HttpStatusCode.TooManyRequests)
                    {
                        _logger.LogWarning("AbuseIPDB rate limit hit while checking IP {IP}", ipAddress);
                        return Enumerable.Empty<Alert>();
                    }

                    response.EnsureSuccessStatusCode();

                    var result = await response.Content.ReadAsStringAsync();
                    var ipData = JsonConvert.DeserializeObject<dynamic>(result);

                    _ipCheckCache.MarkChecked(ipAddress);

                    if (ipData?.data?.abuseConfidenceScore >= _config.ConfidenceMinimum)
                    {
                        var alert = new Alert
                        {
                            Title = "Phát hiện IP đáng ngờ",
                            SourceIp = ipAddress,
                            Description = $"IP đáng ngờ với điểm tin cậy: {ipData.data.abuseConfidenceScore}%",
                            AlertTypeId = (int)AlertTypeId.SuspiciousIP,
                            SeverityLevelId = (int)SeverityLevelId.High,
                            StatusId = (int)AlertStatusId.New,
                            Timestamp = DateTime.UtcNow,
                            Resolution = $"Chi tiết từ AbuseIPDB: {result}"
                        };

                        await _alertService.CreateAlertAsync(alert);
                        await _hubContext.Clients.All.SendAsync("ReceiveAlert", alert);

                        return new[] { alert };
                    }

                    return Enumerable.Empty<Alert>();
                }
                finally
                {
                    _rateLimitSemaphore.Release();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking IP {IP} with AbuseIPDB", ipAddress);
                return Enumerable.Empty<Alert>();
            }
        }

        public async Task<Alert> ReportIPAsync(string ipAddress, string categories, string comment)
        {
            try
            {
                await _rateLimitSemaphore.WaitAsync();
                try
                {
                    await ResetHourlyCounterIfNeeded();

                    if (_hourlyCheckCount >= MAX_HOURLY_CHECKS)
                    {
                        throw new InvalidOperationException("Hourly API limit reached");
                    }

                    _hourlyCheckCount++;
                    var client = CreateClient();
                    var content = new FormUrlEncodedContent(new[]
                    {
                        new KeyValuePair<string, string>("ip", ipAddress),
                        new KeyValuePair<string, string>("categories", categories),
                        new KeyValuePair<string, string>("comment", comment)
                    });

                    var response = await client.PostAsync("/api/v2/report", content);

                    if (response.StatusCode == HttpStatusCode.TooManyRequests)
                    {
                        _logger.LogWarning("AbuseIPDB rate limit hit while reporting IP {IP}", ipAddress);
                        throw new InvalidOperationException("API rate limit reached");
                    }

                    response.EnsureSuccessStatusCode();

                    var alert = new Alert
                    {
                        Title = "IP được báo cáo",
                        SourceIp = ipAddress,
                        Description = $"IP được báo cáo với danh mục: {categories}",
                        AlertTypeId = (int)AlertTypeId.ReportedIP,
                        SeverityLevelId = (int)SeverityLevelId.Medium,
                        StatusId = (int)AlertStatusId.New,
                        Timestamp = DateTime.UtcNow,
                        Resolution = $"Nội dung báo cáo: {comment}"
                    };

                    await _alertService.CreateAlertAsync(alert);
                    await _hubContext.Clients.All.SendAsync("ReceiveAlert", alert);

                    return alert;
                }
                finally
                {
                    _rateLimitSemaphore.Release();
                }
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex.Message);
                throw; // Rethrow rate limit exceptions to let caller handle them
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reporting IP {IP} to AbuseIPDB", ipAddress);
                throw; // Rethrow to match interface contract
            }
        }

        public async Task<IEnumerable<Alert>> GetBlacklistedIPsAsync()
        {
            var alerts = new List<Alert>();

            try
            {
                var client = CreateClient();
                var response = await client.GetAsync($"/api/v2/blacklist?confidenceMinimum={_config.ConfidenceMinimum}");

                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    _logger.LogWarning("AbuseIPDB rate limit hit while fetching blacklist");
                    return alerts;
                }

                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadAsStringAsync();
                var blacklist = JsonConvert.DeserializeObject<dynamic>(result);

                if (blacklist?.data != null)
                {
                    foreach (var ip in blacklist.data)
                    {
                        var alert = new Alert
                        {
                            Title = "IP trong danh sách đen",
                            SourceIp = ip.ipAddress?.ToString(),
                            Description = $"IP trong danh sách đen với điểm tin cậy: {ip.abuseConfidenceScore}%",
                            AlertTypeId = (int)AlertTypeId.BlacklistedIP,
                            SeverityLevelId = (int)SeverityLevelId.Critical,
                            StatusId = (int)AlertStatusId.New,
                            Timestamp = DateTime.UtcNow,
                            Resolution = $"Chi tiết: {JsonConvert.SerializeObject(ip)}"
                        };

                        await _alertService.CreateAlertAsync(alert);
                        await _hubContext.Clients.All.SendAsync("ReceiveAlert", alert);
                        alerts.Add(alert);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting blacklisted IPs from AbuseIPDB");
            }

            return alerts;
        }

        public async Task<Dictionary<string, int>> GetReportStatisticsAsync(TimeSpan window)
        {
            try
            {
                var alerts = await _alertService.GetAllAlertsAsync();
                return alerts
                    .Where(a => a.Timestamp >= DateTime.UtcNow - window)
                    .GroupBy(a => a.SourceIp ?? "unknown")
                    .ToDictionary(g => g.Key, g => g.Count());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting report statistics");
                return new Dictionary<string, int>();
            }
        }

        public async Task<Dictionary<string, double>> GetConfidenceScoresAsync(IEnumerable<string> ipAddresses)
        {
            var result = new Dictionary<string, double>();

            foreach (var ip in ipAddresses)
            {
                try
                {
                    var client = CreateClient();
                    var response = await client.GetAsync($"/api/v2/check?ipAddress={ip}");

                    if (response.StatusCode == HttpStatusCode.TooManyRequests)
                    {
                        _logger.LogWarning("Rate limit hit while getting confidence score for {IP}", ip);
                        result[ip] = -1; // special case to signal rate limited
                        continue;
                    }

                    response.EnsureSuccessStatusCode();
                    var content = await response.Content.ReadAsStringAsync();
                    var ipData = JsonConvert.DeserializeObject<dynamic>(content);
                    result[ip] = ipData?.data?.abuseConfidenceScore ?? 0;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error getting confidence score for IP {IP}", ip);
                    result[ip] = 0;
                }
            }

            return result;
        }

        private HttpClient CreateClient()
        {
            var client = _clientFactory.CreateClient();
            client.BaseAddress = new Uri(_config.ApiBaseUrl);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Add("Key", _config.ApiKey);
            return client;
        }
    }
}
