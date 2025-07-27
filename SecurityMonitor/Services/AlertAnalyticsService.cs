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
    public class AlertAnalyticsService
    {
        private readonly ApplicationDbContext _context;
        private readonly IAlertService _alertService;
        private readonly ILogger<AlertAnalyticsService> _logger;

        public AlertAnalyticsService(
            ApplicationDbContext context,
            IAlertService alertService,
            ILogger<AlertAnalyticsService> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _alertService = alertService ?? throw new ArgumentNullException(nameof(alertService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<IEnumerable<Alert>> GetCorrelatedAlertsAsync(Alert alert, TimeSpan window)
        {
            if (alert == null)
            {
                _logger.LogWarning("Cannot get correlated alerts: Alert is null");
                return Enumerable.Empty<Alert>();
            }

            var cutoffTime = DateTime.UtcNow.Subtract(window);
            
            var correlatedAlerts = await _context.Alerts
                .Include(a => a.AlertType)
                .Include(a => a.SeverityLevel)
                .Where(a => a.Id != alert.Id &&
                           a.Timestamp >= cutoffTime &&
                           ((!string.IsNullOrEmpty(a.SourceIp) && a.SourceIp == alert.SourceIp) ||
                            (!string.IsNullOrEmpty(a.TargetIp) && a.TargetIp == alert.TargetIp) ||
                            a.AlertTypeId == alert.AlertTypeId))
                .OrderByDescending(a => a.Timestamp)
                .ToListAsync();

            return correlatedAlerts;
        }

        public async Task<Dictionary<string, AlertStatistics>> GetNetworkStatisticsAsync(TimeSpan window)
        {
            var cutoffTime = DateTime.UtcNow.Subtract(window);
            var stats = new Dictionary<string, AlertStatistics>();

            try
            {
                var allAlerts = await _context.Alerts
                    .Include(a => a.AlertType)
                    .Include(a => a.SeverityLevel)
                    .Where(a => a.Timestamp >= cutoffTime)
                    .ToListAsync();

                // Phân tích IP nguồn
                var sourceGroups = allAlerts
                    .Where(a => !string.IsNullOrEmpty(a.SourceIp))
                    .GroupBy(a => a.SourceIp);

                foreach (var group in sourceGroups)
                {
                    var key = group.Key;
                    if (key != null)
                    {
                        var ipStats = CalculateAlertStatistics(group.ToList());
                        stats[key] = ipStats;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating network statistics for window {Window}", window);
                throw;
            }

            return stats;
        }

        public async Task<Dictionary<DateTime, NetworkActivitySummary>> GetActivityTrendAsync(
            TimeSpan window, TimeSpan interval)
        {
            var cutoffTime = DateTime.UtcNow.Subtract(window);
            var trend = new Dictionary<DateTime, NetworkActivitySummary>();

            try
            {
                var timeSlots = GenerateTimeSlots(cutoffTime, DateTime.UtcNow, interval);
                var alerts = await _context.Alerts
                    .Include(a => a.AlertType)
                    .Include(a => a.SeverityLevel)
                    .Where(a => a.Timestamp >= cutoffTime)
                    .ToListAsync();

                for (int i = 0; i < timeSlots.Count - 1; i++)
                {
                    var startTime = timeSlots[i];
                    var endTime = timeSlots[i + 1];
                    
                    var periodAlerts = alerts
                        .Where(a => a.Timestamp >= startTime && a.Timestamp < endTime)
                        .ToList();

                    trend[startTime] = new NetworkActivitySummary
                    {
                        TotalAlerts = periodAlerts.Count,
                        UniqueSourceIPs = periodAlerts
                            .Where(a => !string.IsNullOrEmpty(a.SourceIp))
                            .Select(a => a.SourceIp)
                            .Distinct()
                            .Count(),
                        UniqueTargetIPs = periodAlerts
                            .Where(a => !string.IsNullOrEmpty(a.TargetIp))
                            .Select(a => a.TargetIp)
                            .Distinct()
                            .Count(),
                        CriticalAlerts = periodAlerts.Count(a => a.SeverityLevelId >= 3),
                        TopAlertTypes = periodAlerts
                            .Where(a => a.AlertType != null)
                            .GroupBy(a => a.AlertType!.Name)
                            .OrderByDescending(g => g.Count())
                            .Take(3)
                            .ToDictionary(g => g.Key, g => g.Count())
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating activity trend for window {Window}", window);
                throw;
            }

            return trend;
        }

        private List<DateTime> GenerateTimeSlots(DateTime start, DateTime end, TimeSpan interval)
        {
            var slots = new List<DateTime>();
            var current = start;

            while (current <= end)
            {
                slots.Add(current);
                current = current.Add(interval);
            }

            return slots;
        }

        private AlertStatistics CalculateAlertStatistics(List<Alert> alerts)
        {
            return new AlertStatistics
            {
                TotalAlerts = alerts.Count,
                CriticalAlerts = alerts.Count(a => a.SeverityLevelId >= 3),
                UniqueTargets = alerts
                    .Where(a => !string.IsNullOrEmpty(a.TargetIp))
                    .Select(a => a.TargetIp)
                    .Distinct()
                    .Count(),
                AlertTypeDistribution = alerts
                    .Where(a => a.AlertType?.Name != null)
                    .GroupBy(a => a.AlertType!.Name)
                    .ToDictionary(g => g.Key, g => g.Count()),
                AverageAlertSeverity = alerts.Average(a => a.SeverityLevelId),
                LastSeenAt = alerts.Max(a => a.Timestamp),
                RecentActivity = alerts
                    .Where(a => a.Timestamp >= DateTime.UtcNow.AddHours(-1))
                    .Count()
            };
        }
    }

    public class AlertStatistics
    {
        public int TotalAlerts { get; set; }
        public int CriticalAlerts { get; set; }
        public int UniqueTargets { get; set; }
        public Dictionary<string, int> AlertTypeDistribution { get; set; } = new();
        public double AverageAlertSeverity { get; set; }
        public DateTime LastSeenAt { get; set; }
        public int RecentActivity { get; set; }
    }

    public class NetworkActivitySummary
    {
        public int TotalAlerts { get; set; }
        public int UniqueSourceIPs { get; set; }
        public int UniqueTargetIPs { get; set; }
        public int CriticalAlerts { get; set; }
        public Dictionary<string, int> TopAlertTypes { get; set; } = new();
    }
}
