using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SecurityMonitor.Data;
using SecurityMonitor.Models;
using SecurityMonitor.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SecurityMonitor.Services
{
    public class AlertService : IAlertService
    {
        private readonly ApplicationDbContext _context;
        private readonly SecurityMonitor.Services.Interfaces.IAuditService _auditService;
        private readonly ILogger<AlertService> _logger;

        public AlertService(
            ApplicationDbContext context,
            SecurityMonitor.Services.Interfaces.IAuditService auditService,
            ILogger<AlertService> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<IEnumerable<Alert>> GetAllAlertsAsync()
        {
            try
            {
                return await _context.Alerts
                    .Include(a => a.AlertType)
                    .Include(a => a.SeverityLevel)
                    .Include(a => a.Status)
                    .Include(a => a.AssignedTo)
                    .OrderByDescending(a => a.Timestamp)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy tất cả cảnh báo");
                throw;
            }
        }

        public async Task<Alert?> GetAlertByIdAsync(int id)
        {
            try
            {
                return await _context.Alerts
                    .Include(a => a.AlertType)
                    .Include(a => a.SeverityLevel)
                    .Include(a => a.Status)
                    .Include(a => a.AssignedTo)
                    .FirstOrDefaultAsync(a => a.Id == id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy cảnh báo có ID {AlertId}", id);
                throw;
            }
        }

        public async Task<Alert> CreateAlertAsync(Alert alert)
        {
            try
            {
                if (alert == null)
                    throw new ArgumentNullException(nameof(alert));

                alert.Timestamp = DateTime.UtcNow;
                _context.Alerts.Add(alert);
                await _context.SaveChangesAsync();

                await _auditService.LogActivityAsync(
                    alert.AssignedToId ?? "System",
                    "Create",
                    "Alert",
                    alert.Id.ToString(),
                    $"Đã tạo cảnh báo: {alert.Title}"
                );

                return alert;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi tạo cảnh báo: {AlertTitle}", alert?.Title);
                throw;
            }
        }

        public async Task<Alert?> UpdateAlertAsync(int id, Alert alert)
        {
            try
            {
                if (alert == null)
                    throw new ArgumentNullException(nameof(alert));

                var existingAlert = await _context.Alerts.FindAsync(id);
                if (existingAlert == null) return null;

                existingAlert.Title = alert.Title;
                existingAlert.Description = alert.Description;
                existingAlert.AlertTypeId = alert.AlertTypeId;
                existingAlert.SeverityLevelId = alert.SeverityLevelId;
                existingAlert.StatusId = alert.StatusId;

                await _context.SaveChangesAsync();

                await _auditService.LogActivityAsync(
                    alert.AssignedToId ?? "System",
                    "Update",
                    "Alert",
                    alert.Id.ToString(),
                    $"Đã cập nhật cảnh báo: {alert.Title}"
                );

                return existingAlert;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi cập nhật cảnh báo có ID {AlertId}", id);
                throw;
            }
        }

        public async Task<bool> DeleteAlertAsync(int id)
        {
            try
            {
                var alert = await _context.Alerts.FindAsync(id);
                if (alert == null) return false;

                _context.Alerts.Remove(alert);
                await _context.SaveChangesAsync();

                await _auditService.LogActivityAsync(
                    "System",
                    "Delete",
                    "Alert",
                    id.ToString(),
                    $"Đã xóa cảnh báo: {alert.Title}"
                );

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi xóa cảnh báo có ID {AlertId}", id);
                throw;
            }
        }

        public async Task<List<Alert>> GetRecentAlertsBySourceIp(string sourceIp, TimeSpan timeWindow)
        {
            try
            {
                if (string.IsNullOrEmpty(sourceIp))
                    return new List<Alert>();

                var cutoffTime = DateTime.UtcNow.Subtract(timeWindow);
                return await _context.Alerts
                    .Include(a => a.AlertType)
                    .Include(a => a.SeverityLevel)
                    .Where(a => a.SourceIp == sourceIp && a.Timestamp >= cutoffTime)
                    .OrderByDescending(a => a.Timestamp)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy các cảnh báo gần đây cho IP nguồn {SourceIp}", sourceIp);
                throw;
            }
        }

        public async Task<int> GetAlertCountInTimeRange(string sourceIp, TimeSpan timeWindow)
        {
            try
            {
                if (string.IsNullOrEmpty(sourceIp))
                    return 0;

                var cutoffTime = DateTime.UtcNow.Subtract(timeWindow);
                return await _context.Alerts
                    .CountAsync(a => a.SourceIp == sourceIp && a.Timestamp >= cutoffTime);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi đếm số cảnh báo cho IP nguồn {SourceIp}", sourceIp);
                throw;
            }
        }

        public async Task<Dictionary<string, int>> GetAlertTypeFrequency(string sourceIp, TimeSpan timeWindow)
        {
            try
            {
                if (string.IsNullOrEmpty(sourceIp))
                    return new Dictionary<string, int>();

                var cutoffTime = DateTime.UtcNow.Subtract(timeWindow);
                var alerts = await _context.Alerts
                    .Include(a => a.AlertType)
                    .Where(a => a.SourceIp == sourceIp && a.Timestamp >= cutoffTime)
                    .ToListAsync();

                return alerts
                    .Where(a => a.AlertType != null)
                    .GroupBy(a => a.AlertType!.Name)
                    .ToDictionary(g => g.Key, g => g.Count());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy tần suất loại cảnh báo cho IP nguồn {SourceIp}", sourceIp);
                throw;
            }
        }

        public async Task<IEnumerable<Alert>> GetAlertsBySeverityAsync(int severityLevelId)
        {
            try
            {
                return await _context.Alerts
                    .Where(a => a.SeverityLevelId == severityLevelId)
                    .Include(a => a.AlertType)
                    .Include(a => a.Status)
                    .OrderByDescending(a => a.Timestamp)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy cảnh báo theo mức độ nghiêm trọng {SeverityLevelId}", severityLevelId);
                throw;
            }
        }

        public async Task<IEnumerable<Alert>> GetAlertsByStatusAsync(int statusId)
        {
            try
            {
                return await _context.Alerts
                    .Where(a => a.StatusId == statusId)
                    .Include(a => a.AlertType)
                    .Include(a => a.SeverityLevel)
                    .OrderByDescending(a => a.Timestamp)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy cảnh báo theo trạng thái {StatusId}", statusId);
                throw;
            }
        }

        public async Task<Alert?> AssignAlertAsync(int alertId, string userId)
        {
            try
            {
                var alert = await _context.Alerts.FindAsync(alertId);
                if (alert == null) return null;

                alert.AssignedToId = userId;
                alert.StatusId = AlertConstants.AlertStatus.Processing;
                await _context.SaveChangesAsync();

                await _auditService.LogActivityAsync(
                    userId,
                    "Assign",
                    "Alert",
                    alertId.ToString(),
                    $"Đã phân công cảnh báo cho người dùng"
                );

                return alert;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi phân công cảnh báo {AlertId} cho người dùng {UserId}", alertId, userId);
                throw;
            }
        }

        public async Task<Alert?> ResolveAlertAsync(int alertId, string userId, string resolution)
        {
            try
            {
                var alert = await _context.Alerts.FindAsync(alertId);
                if (alert == null) return null;

                alert.ResolvedById = userId;
                alert.ResolvedAt = DateTime.UtcNow;
                alert.Resolution = resolution;
                alert.StatusId = AlertConstants.AlertStatus.Resolved;

                await _context.SaveChangesAsync();

                await _auditService.LogActivityAsync(
                    userId,
                    "Resolve",
                    "Alert",
                    alertId.ToString(),
                    $"Đã giải quyết cảnh báo với giải pháp: {resolution}"
                );

                return alert;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi giải quyết cảnh báo {AlertId}", alertId);
                throw;
            }
        }

        public async Task<IEnumerable<Alert>> GetCorrelatedAlertsAsync(Alert alert, TimeSpan window)
        {
            try
            {
                if (alert == null)
                {
                    _logger.LogWarning("Không thể lấy cảnh báo tương quan: Cảnh báo rỗng");
                    return Enumerable.Empty<Alert>();
                }

                var cutoffTime = DateTime.UtcNow.Subtract(window);

                return await _context.Alerts
                    .Include(a => a.AlertType)
                    .Include(a => a.SeverityLevel)
                    .Where(a => a.Id != alert.Id &&
                               a.Timestamp >= cutoffTime &&
                               ((!string.IsNullOrEmpty(a.SourceIp) && a.SourceIp == alert.SourceIp) ||
                                (!string.IsNullOrEmpty(a.TargetIp) && a.TargetIp == alert.TargetIp) ||
                                a.AlertTypeId == alert.AlertTypeId))
                    .OrderByDescending(a => a.Timestamp)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy cảnh báo tương quan cho cảnh báo {AlertId}", alert?.Id);
                throw;
            }
        }

        public async Task<IEnumerable<Alert>> GetAlertsByIpRangeAsync(string ipPrefix, TimeSpan window)
        {
            try
            {
                if (string.IsNullOrEmpty(ipPrefix))
                {
                    _logger.LogWarning("Không thể lấy cảnh báo theo dải IP: Tiền tố IP rỗng");
                    return Enumerable.Empty<Alert>();
                }

                var cutoffTime = DateTime.UtcNow.Subtract(window);

                return await _context.Alerts
                    .Include(a => a.AlertType)
                    .Include(a => a.SeverityLevel)
                    .Where(a => a.Timestamp >= cutoffTime &&
                               ((!string.IsNullOrEmpty(a.SourceIp) && a.SourceIp.StartsWith(ipPrefix)) ||
                                (!string.IsNullOrEmpty(a.TargetIp) && a.TargetIp.StartsWith(ipPrefix))))
                    .OrderByDescending(a => a.Timestamp)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy cảnh báo cho dải IP {IpPrefix}", ipPrefix);
                throw;
            }
        }

        public async Task<Dictionary<string, int>> GetAlertStatisticsAsync(TimeSpan window)
        {
            try
            {
                var cutoffTime = DateTime.UtcNow.Subtract(window);
                var stats = new Dictionary<string, int>();

                // Thống kê theo loại cảnh báo
                var alertTypes = await _context.Alerts
                    .Where(a => a.Timestamp >= cutoffTime)
                    .GroupBy(a => a.AlertTypeId)
                    .Select(g => new { TypeId = g.Key, Count = g.Count() })
                    .ToListAsync();

                foreach (var type in alertTypes)
                {
                    var alertType = await _context.AlertTypes.FindAsync(type.TypeId);
                    if (alertType != null)
                    {
                        stats[$"Type_{alertType.Name}"] = type.Count;
                    }
                }

                // Thống kê theo mức độ nghiêm trọng
                var severities = await _context.Alerts
                    .Where(a => a.Timestamp >= cutoffTime)
                    .GroupBy(a => a.SeverityLevelId)
                    .Select(g => new { SeverityId = g.Key, Count = g.Count() })
                    .ToListAsync();

                foreach (var severity in severities)
                {
                    var severityLevel = await _context.SeverityLevels.FindAsync(severity.SeverityId);
                    if (severityLevel != null)
                    {
                        stats[$"Severity_{severityLevel.Name}"] = severity.Count;
                    }
                }

                // Thống kê tổng quan
                stats["Total"] = await _context.Alerts.CountAsync(a => a.Timestamp >= cutoffTime);
                stats["Unresolved"] = await _context.Alerts.CountAsync(a => a.Timestamp >= cutoffTime && a.ResolvedById == null);
                stats["Critical"] = await _context.Alerts.CountAsync(a => a.Timestamp >= cutoffTime && a.SeverityLevelId >= 3);

                return stats;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy thống kê cảnh báo cho khoảng thời gian {Window}", window);
                throw;
            }
        }

        public async Task<Dictionary<DateTime, int>> GetAlertTrendAsync(TimeSpan window, TimeSpan interval)
        {
            try
            {
                var cutoffTime = DateTime.UtcNow.Subtract(window);
                var trend = new Dictionary<DateTime, int>();

                // Tạo các khoảng thời gian
                var timeSlots = new List<DateTime>();
                var currentTime = cutoffTime;
                while (currentTime <= DateTime.UtcNow)
                {
                    timeSlots.Add(currentTime);
                    currentTime = currentTime.Add(interval);
                }

                // Đếm số cảnh báo trong mỗi khoảng
                for (int i = 0; i < timeSlots.Count - 1; i++)
                {
                    var startTime = timeSlots[i];
                    var endTime = timeSlots[i + 1];

                    var count = await _context.Alerts
                        .CountAsync(a => a.Timestamp >= startTime && a.Timestamp < endTime);

                    trend[startTime] = count;
                }

                return trend;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy xu hướng cảnh báo cho khoảng thời gian {Window} với khoảng {Interval}", 
                    window, interval);
                throw;
            }
        }

        public async Task<Dictionary<string, double>> GetThreatScoreBySourceAsync(TimeSpan window)
        {
            try
            {
                var cutoffTime = DateTime.UtcNow.Subtract(window);
                var scores = new Dictionary<string, double>();

                var sourceIps = await _context.Alerts
                    .Where(a => a.Timestamp >= cutoffTime && !string.IsNullOrEmpty(a.SourceIp))
                    .Select(a => a.SourceIp)
                    .Distinct()
                    .ToListAsync();

                foreach (var ip in sourceIps)
                {
                    if (!string.IsNullOrEmpty(ip))
                    {
                        var alerts = await _context.Alerts
                            .Include(a => a.SeverityLevel)
                            .Where(a => a.Timestamp >= cutoffTime && a.SourceIp == ip)
                            .ToListAsync();

                        scores[ip] = CalculateThreatScore(alerts);
                    }
                }

                return scores.OrderByDescending(s => s.Value)
                    .ToDictionary(s => s.Key, s => s.Value);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi tính điểm đe dọa cho các nguồn trong khoảng thời gian {Window}", window);
                throw;
            }
        }

        public async Task<Dictionary<string, double>> GetThreatScoreByTargetAsync(TimeSpan window)
        {
            try
            {
                var cutoffTime = DateTime.UtcNow.Subtract(window);
                var scores = new Dictionary<string, double>();

                var targetIps = await _context.Alerts
                    .Where(a => a.Timestamp >= cutoffTime && !string.IsNullOrEmpty(a.TargetIp))
                    .Select(a => a.TargetIp)
                    .Distinct()
                    .ToListAsync();

                foreach (var ip in targetIps)
                {
                    if (!string.IsNullOrEmpty(ip))
                    {
                        var alerts = await _context.Alerts
                            .Include(a => a.SeverityLevel)
                            .Where(a => a.Timestamp >= cutoffTime && a.TargetIp == ip)
                            .ToListAsync();

                        scores[ip] = CalculateThreatScore(alerts);
                    }
                }

                return scores.OrderByDescending(s => s.Value)
                    .ToDictionary(s => s.Key, s => s.Value);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi tính điểm đe dọa cho các mục tiêu trong khoảng thời gian {Window}", window);
                throw;
            }
        }

        private double CalculateThreatScore(List<Alert> alerts)
        {
            if (!alerts.Any()) return 0;

            // Tính điểm dựa trên số lượng cảnh báo
            double volumeScore = Math.Min(alerts.Count / 10.0, 1.0);

            // Tính điểm dựa trên mức độ nghiêm trọng
            double severityScore = alerts.Average(a => a.SeverityLevelId) / 4.0;

            // Tính điểm dựa trên tần suất (trong 1 giờ gần nhất)
            var recentAlerts = alerts.Count(a => a.Timestamp >= DateTime.UtcNow.AddHours(-1));
            double frequencyScore = Math.Min(recentAlerts / 5.0, 1.0);

            // Trọng số cho mỗi thành phần
            const double VOLUME_WEIGHT = 0.3;
            const double SEVERITY_WEIGHT = 0.4;
            const double FREQUENCY_WEIGHT = 0.3;

            // Tính điểm tổng hợp (0-100)
            return (volumeScore * VOLUME_WEIGHT +
                    severityScore * SEVERITY_WEIGHT +
                    frequencyScore * FREQUENCY_WEIGHT) * 100;
        }

        public async Task<AlertStatus?> GetAlertStatusByNameAsync(string statusName)
        {
            try
            {
                return await _context.AlertStatuses
                    .FirstOrDefaultAsync(s => s.Name.ToLower() == statusName.ToLower());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy trạng thái cảnh báo theo tên {StatusName}", statusName);
                throw;
            }
        }
    }
}
