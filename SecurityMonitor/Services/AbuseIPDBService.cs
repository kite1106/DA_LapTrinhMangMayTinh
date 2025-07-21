using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using SecurityMonitor.Hubs;
using SecurityMonitor.Models;
using SecurityMonitor.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace SecurityMonitor.Services
{
    public class AbuseIPDBService : IAbuseIPDBService
    {
        private readonly AbuseIPDBConfig _config;
        private readonly IHttpClientFactory _clientFactory;
        private readonly IAlertService _alertService;
        private readonly ILogger<AbuseIPDBService> _logger;
        private readonly IHubContext<AlertHub> _hubContext;

        public AbuseIPDBService(
            IOptions<AbuseIPDBConfig> config,
            IHttpClientFactory clientFactory,
            IAlertService alertService,
            ILogger<AbuseIPDBService> logger,
            IHubContext<AlertHub> hubContext)
        {
            _config = config.Value;
            _clientFactory = clientFactory;
            _alertService = alertService;
            _logger = logger;
            _hubContext = hubContext;
        }

        private HttpClient CreateClient()
        {
            var client = _clientFactory.CreateClient();
            client.BaseAddress = new Uri(_config.ApiBaseUrl);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Add("Key", _config.ApiKey);
            return client;
        }

        public async Task<IEnumerable<Alert>> CheckIPAsync(string ipAddress)
        {
            var alerts = new List<Alert>();
            try
            {
                var client = CreateClient();
                var response = await client.GetAsync($"/api/v2/check?ipAddress={ipAddress}");
                if (response.StatusCode == HttpStatusCode.TooManyRequests) return alerts;

                response.EnsureSuccessStatusCode();
                var content = await response.Content.ReadAsStringAsync();
                dynamic data = JsonConvert.DeserializeObject<dynamic>(content);
                int score = data?.data?.abuseConfidenceScore ?? 0;

                if (score >= _config.ConfidenceMinimum)
                {
                    var alert = new Alert
                    {
                        Title = "Phát hiện IP đáng ngờ",
                        SourceIp = ipAddress,
                        Description = $"IP có điểm tin cậy cao ({score}%)",
                        AlertTypeId = (int)AlertTypeId.SuspiciousIP,
                        SeverityLevelId = (int)SeverityLevelId.High,
                        StatusId = (int)AlertStatusId.New,
                        Timestamp = DateTime.UtcNow
                    };

                    alerts.Add(alert);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi kiểm tra IP {Ip}", ipAddress);
            }

            return alerts;
        }

        public async Task<Alert> ReportIPAsync(string ipAddress, string categories, string comment)
        {
            var client = CreateClient();
            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "ip", ipAddress },
                { "categories", categories },
                { "comment", comment }
            });

            var response = await client.PostAsync("/api/v2/report", content);
            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadAsStringAsync();
            dynamic data = JsonConvert.DeserializeObject<dynamic>(body);

            return new Alert
            {
                Title = "Đã báo cáo IP đáng ngờ",
                SourceIp = ipAddress,
                Description = $"Báo cáo danh mục: {categories}",
                AlertTypeId = (int)AlertTypeId.ReportedIP,
                SeverityLevelId = (int)SeverityLevelId.Medium,
                StatusId = (int)AlertStatusId.New,
                Timestamp = DateTime.UtcNow,
                Resolution = JsonConvert.SerializeObject(data)
            };
        }

        public async Task<Dictionary<string, int>> GetReportStatisticsAsync(TimeSpan window)
        {
            var stats = new Dictionary<string, int>();
            try
            {
                var client = CreateClient();
                var response = await client.GetAsync("/api/v2/stats");
                response.EnsureSuccessStatusCode();
                var body = await response.Content.ReadAsStringAsync();
                dynamic data = JsonConvert.DeserializeObject<dynamic>(body);

                stats["TotalReports"] = data?.data?.totalReports ?? 0;
                stats["DistinctIPs"] = data?.data?.numDistinctIp ?? 0;
                stats["ReportedToday"] = data?.data?.reportsToday ?? 0;
                stats["Whitelisted"] = (data?.data?.isWhitelisted ?? false) ? 1 : 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi thống kê AbuseIPDB");
            }

            return stats;
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
                    if (response.StatusCode == HttpStatusCode.TooManyRequests) continue;

                    response.EnsureSuccessStatusCode();
                    var body = await response.Content.ReadAsStringAsync();
                    dynamic data = JsonConvert.DeserializeObject<dynamic>(body);
                    result[ip] = data?.data?.abuseConfidenceScore ?? 0;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi lấy điểm tin cậy IP {Ip}", ip);
                }
            }

            return result;
        }

        public async Task<IEnumerable<Alert>> GetBlacklistedIPsAsync() => new List<Alert>(); // Không dùng nữa
    }
}