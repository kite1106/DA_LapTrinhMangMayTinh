using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Identity.Client;
using Newtonsoft.Json;
using SecurityMonitor.Models;
using SecurityMonitor.Hubs;
using SecurityMonitor.Services.Interfaces;

namespace SecurityMonitor.Services
{
    public class DefenderAlertAnalytics
    {
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _clientFactory;
        private readonly SecurityMonitor.Services.Interfaces.IAlertService _alertService;
        private readonly SecurityMonitor.Services.Interfaces.IAuditService _auditService;
        private readonly ILogger<DefenderAlertAnalytics> _logger;
        private readonly TimeZoneInfo _localTimeZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");

        public DefenderAlertAnalytics(
            IConfiguration configuration,
            IHttpClientFactory clientFactory,
            SecurityMonitor.Services.Interfaces.IAlertService alertService,
            SecurityMonitor.Services.Interfaces.IAuditService auditService,
            ILogger<DefenderAlertAnalytics> logger)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
            _alertService = alertService ?? throw new ArgumentNullException(nameof(alertService));
            _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Phân tích thời gian bất thường của cảnh báo (ngoài giờ làm việc, cuối tuần, mẫu lặp).
        /// </summary>
        public async Task EnrichTimingAnalysis(Alert alert)
        {
            if (alert == null)
            {
                _logger.LogWarning("Không thể phân tích thời gian: Cảnh báo rỗng");
                return;
            }

            try
            {
                var timing = new List<string>();
                var localTime = TimeZoneInfo.ConvertTimeFromUtc(alert.Timestamp.ToUniversalTime(), _localTimeZone);

                var hour = localTime.Hour;
                if (hour < AlertConstants.WorkingHours.Start || hour > AlertConstants.WorkingHours.End)
                {
                    timing.Add($"Cảnh báo xảy ra ngoài giờ làm việc (lúc {hour:00}:00)");
                }

                if (localTime.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
                {
                    timing.Add("Cảnh báo xảy ra vào cuối tuần");
                }

                if (!string.IsNullOrEmpty(alert.SourceIp))
                {
                    var timePattern = await AnalyzeTimePattern(alert.SourceIp);
                    if (!string.IsNullOrEmpty(timePattern))
                    {
                        timing.Add($"Phát hiện mẫu thời gian: {timePattern}");
                    }
                }

                if (timing.Any())
                {
                    alert.Description += "\n\nPhân Tích Thời Gian:\n- " + string.Join("\n- ", timing);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi phân tích thời gian cho cảnh báo {AlertId}", alert.Id);
                alert.Description += "\n\nPhân Tích Thời Gian: Có lỗi xảy ra khi phân tích mẫu thời gian.";
            }
        }

        /// <summary>
        /// Phân tích tần suất xảy ra cảnh báo từ IP nguồn.
        /// </summary>
        public async Task EnrichFrequencyAnalysis(Alert alert)
        {
            if (alert == null || string.IsNullOrEmpty(alert.SourceIp))
            {
                _logger.LogWarning("Không thể phân tích tần suất: Cảnh báo rỗng hoặc IP nguồn không hợp lệ.");
                return;
            }

            try
            {
                var frequency = new List<string>();
                var intervals = new[]
                {
                    TimeSpan.FromMinutes(5),
                    TimeSpan.FromMinutes(30),
                    TimeSpan.FromHours(1),
                    TimeSpan.FromHours(24)
                };

                foreach (var interval in intervals)
                {
                    var count = await _alertService.GetAlertCountInTimeRange(alert.SourceIp, interval);
                    var threshold = AlertConstants.ThresholdConfig.MaxAlertsPerHour * interval.TotalHours;
                    if (count > threshold)
                    {
                        frequency.Add($"Phát hiện tần suất cao: {count} cảnh báo trong {interval.TotalHours:F1} giờ qua (ngưỡng: {threshold:F0})");
                    }
                }

                var typeFrequency = await _alertService.GetAlertTypeFrequency(alert.SourceIp, TimeSpan.FromHours(24));
                if (typeFrequency.Count > 1)
                {
                    frequency.Add($"Phát hiện nhiều loại tấn công ({typeFrequency.Count}): " +
                                  string.Join(", ", typeFrequency.Select(kvp => $"{kvp.Key} ({kvp.Value})")));
                }

                if (frequency.Any())
                {
                    alert.Description += "\n\nPhân Tích Tần Suất:\n- " + string.Join("\n- ", frequency);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi phân tích tần suất cho cảnh báo {AlertId} từ {SourceIp}", alert.Id, alert.SourceIp);
                alert.Description += "\n\nPhân Tích Tần Suất: Có lỗi xảy ra khi phân tích tần suất cảnh báo.";
            }
        }

        /// <summary>
        /// Phân tích mẫu thời gian từ các cảnh báo gần đây của một IP.
        /// </summary>
        private async Task<string> AnalyzeTimePattern(string sourceIp)
        {
            if (string.IsNullOrEmpty(sourceIp))
            {
                _logger.LogWarning("Không thể phân tích mẫu thời gian: IP nguồn rỗng");
                return string.Empty;
            }

            try
            {
                var alerts = await _alertService.GetRecentAlertsBySourceIp(sourceIp, TimeSpan.FromDays(7));
                if (!alerts.Any())
                {
                    _logger.LogInformation("Không tìm thấy cảnh báo nào cho IP {SourceIp} trong 7 ngày qua", sourceIp);
                    return string.Empty;
                }

                var orderedAlerts = alerts.OrderBy(a => a.Timestamp).ToList();
                var intervals = new List<TimeSpan>();

                for (int i = 1; i < orderedAlerts.Count; i++)
                {
                    intervals.Add(orderedAlerts[i].Timestamp - orderedAlerts[i - 1].Timestamp);
                }

                var commonIntervals = intervals
                    .GroupBy(i => Math.Round(i.TotalMinutes))
                    .OrderByDescending(g => g.Count())
                    .FirstOrDefault();

                if (commonIntervals?.Count() >= 3)
                {
                    _logger.LogInformation("Phát hiện mẫu khoảng thời gian cho IP {SourceIp}: {Interval} phút", sourceIp, commonIntervals.Key);
                    return $"Khoảng thời gian đều đặn {commonIntervals.Key} phút giữa các cảnh báo";
                }

                var hourDistribution = alerts
                    .GroupBy(a => TimeZoneInfo.ConvertTimeFromUtc(a.Timestamp.ToUniversalTime(), _localTimeZone).Hour)
                    .OrderByDescending(g => g.Count())
                    .ToList();

                if (hourDistribution.Any())
                {
                    var mostCommonHour = hourDistribution.First();
                    var concentration = (double)mostCommonHour.Count() / alerts.Count;
                    if (concentration >= 0.5)
                    {
                        _logger.LogInformation("Phát hiện tập trung thời gian {Hour}: {Percent:P0}", mostCommonHour.Key, concentration);
                        return $"Hoạt động tập trung vào lúc {mostCommonHour.Key:00}:00 ({concentration:P0} cảnh báo)";
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi phân tích mẫu thời gian cho IP {SourceIp}", sourceIp);
            }

            return string.Empty;
        }

        /// <summary>
        /// Phân tích mối liên hệ giữa cảnh báo hiện tại và các cảnh báo khác trong hệ thống.
        /// </summary>
        public async Task EnrichCorrelationAnalysis(Alert alert)
        {
            if (alert == null)
            {
                _logger.LogWarning("Không thể phân tích tương quan: Cảnh báo rỗng");
                return;
            }

            try
            {
                var correlations = new List<string>();

                var relatedAlerts = await _alertService.GetCorrelatedAlertsAsync(alert, TimeSpan.FromHours(24));
                if (relatedAlerts.Any())
                {
                    correlations.Add($"Tìm thấy {relatedAlerts.Count()} cảnh báo liên quan trong 24 giờ qua");

                    var byType = relatedAlerts
                        .Where(a => a.AlertType != null)
                        .GroupBy(a => a.AlertType!.Name)
                        .OrderByDescending(g => g.Count());

                    foreach (var group in byType.Take(3))
                    {
                        correlations.Add($"- Loại {group.Key}: {group.Count()} cảnh báo");
                    }
                }

                if (!string.IsNullOrEmpty(alert.SourceIp))
                {
                    var ipPrefix = alert.SourceIp.Substring(0, alert.SourceIp.LastIndexOf('.'));
                    var rangeAlerts = await _alertService.GetAlertsByIpRangeAsync(ipPrefix, TimeSpan.FromHours(24));
                    if (rangeAlerts.Any())
                    {
                        correlations.Add($"Phát hiện {rangeAlerts.Count()} cảnh báo từ dải mạng {ipPrefix}.*");
                    }
                }

                if (correlations.Any())
                {
                    alert.Description += "\n\nPhân Tích Tương Quan:\n- " + string.Join("\n- ", correlations);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi phân tích tương quan cho cảnh báo {AlertId}", alert.Id);
                alert.Description += "\n\nPhân Tích Tương Quan: Có lỗi xảy ra khi phân tích mối tương quan.";
            }
        }

        /// <summary>
        /// Đánh giá mức độ mối đe dọa của IP nguồn và mục tiêu dựa trên dữ liệu lịch sử.
        /// </summary>
        public async Task EnrichThreatAssessment(Alert alert)
        {
            if (alert == null)
            {
                _logger.LogWarning("Không thể đánh giá mối đe dọa: Cảnh báo rỗng");
                return;
            }

            try
            {
                var assessment = new List<string>();

                if (!string.IsNullOrEmpty(alert.SourceIp))
                {
                    var sourceScores = await _alertService.GetThreatScoreBySourceAsync(TimeSpan.FromDays(7));
                    if (sourceScores.TryGetValue(alert.SourceIp, out double score))
                    {
                        var level = score switch
                        {
                            >= 80 => "Cực kỳ nguy hiểm",
                            >= 60 => "Nguy hiểm cao",
                            >= 40 => "Nguy hiểm",
                            >= 20 => "Đáng ngờ",
                            _ => "Thấp"
                        };
                        assessment.Add($"Mức độ nguy hiểm của nguồn: {level} (điểm: {score:F1})");
                    }
                }

                if (!string.IsNullOrEmpty(alert.TargetIp))
                {
                    var targetScores = await _alertService.GetThreatScoreByTargetAsync(TimeSpan.FromDays(7));
                    if (targetScores.TryGetValue(alert.TargetIp, out double targetScore))
                    {
                        assessment.Add($"Mức độ bị tấn công của mục tiêu: {targetScore:F1} điểm");
                    }
                }

                var stats = await _alertService.GetAlertStatisticsAsync(TimeSpan.FromHours(24));
                if (stats.TryGetValue("Critical", out int criticalCount))
                {
                    assessment.Add($"Số cảnh báo nghiêm trọng trong 24h qua: {criticalCount}");
                }

                if (assessment.Any())
                {
                    alert.Description += "\n\nĐánh Giá Mối Đe Dọa:\n- " + string.Join("\n- ", assessment);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi đánh giá mối đe dọa cho cảnh báo {AlertId}", alert.Id);
                alert.Description += "\n\nĐánh Giá Mối Đe Dọa: Có lỗi xảy ra khi đánh giá mức độ nguy hiểm.";
            }
        }
    }
}
