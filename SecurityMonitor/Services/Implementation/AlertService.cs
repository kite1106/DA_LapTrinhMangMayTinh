using SecurityMonitor.Models;
using SecurityMonitor.Data;
using SecurityMonitor.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using SecurityMonitor.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace SecurityMonitor.Services.Implementation
{
    public class AlertService : IAlertService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AlertService> _logger;
        private readonly IHubContext<AlertHub> _hubContext;

        public AlertService(
            ApplicationDbContext context,
            ILogger<AlertService> logger,
            IHubContext<AlertHub> hubContext)
        {
            _context = context;
            _logger = logger;
            _hubContext = hubContext;
        }

        public async Task<IEnumerable<Alert>> GetAllAlertsAsync()
        {
            return await _context.Alerts
                .OrderByDescending(a => a.Timestamp)
                .ToListAsync();
        }

        public async Task<IEnumerable<Alert>> GetUserAlertsAsync(string userId)
        {
            return await _context.Alerts
                .Where(a => a.AssignedToId == userId)
                .OrderByDescending(a => a.Timestamp)
                .ToListAsync();
        }

        public async Task<Alert?> GetAlertByIdAsync(int id)
        {
            return await _context.Alerts
                .Include(a => a.SeverityLevel)
                .Include(a => a.AlertType)
                .FirstOrDefaultAsync(a => a.Id == id);
        }

        public async Task<Alert> CreateAlertAsync(Alert alert)
        {
            alert.Timestamp = DateTime.UtcNow;
            _context.Alerts.Add(alert);
            await _context.SaveChangesAsync();

            await _hubContext.Clients.All.SendAsync("ReceiveAlert", new
            {
                alert.Id,
                alert.Title,
                alert.Description,
                alert.SourceIp,
                alert.Timestamp,
                SeverityLevel = alert.SeverityLevelId.ToString(),
                Type = alert.AlertTypeId.ToString()
            });

            _logger.LogInformation("Created new alert: {Title}", alert.Title);
            return alert;
        }

        public async Task<Alert?> UpdateAlertAsync(int id, Alert alert)
        {
            var existingAlert = await _context.Alerts.FindAsync(id);
            if (existingAlert == null) return null;

            existingAlert.Title = alert.Title;
            existingAlert.Description = alert.Description;
            existingAlert.SeverityLevelId = alert.SeverityLevelId;
            existingAlert.AlertTypeId = alert.AlertTypeId;
            existingAlert.StatusId = alert.StatusId;

            await _context.SaveChangesAsync();
            return existingAlert;
        }

        public async Task<bool> DeleteAlertAsync(int id)
        {
            var alert = await _context.Alerts.FindAsync(id);
            if (alert == null) return false;

            _context.Alerts.Remove(alert);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> GetRecentAlertByIpAsync(string ip, TimeSpan timeWindow)
        {
            var cutoffTime = DateTime.UtcNow.Subtract(timeWindow);
            return await _context.Alerts
                .AnyAsync(a => a.SourceIp == ip && a.Timestamp >= cutoffTime);
        }

        public async Task<bool> AlertExistsAsync(string ip, AlertTypeId alertTypeId)
        {
            return await _context.Alerts
                .AnyAsync(a => a.SourceIp == ip && a.AlertTypeId == (int)alertTypeId);
        }

        public async Task<List<Alert>> GetRecentAlertsBySourceIp(string sourceIp, TimeSpan timeWindow)
        {
            var cutoffTime = DateTime.UtcNow.Subtract(timeWindow);
            return await _context.Alerts
                .Where(a => a.SourceIp == sourceIp && a.Timestamp >= cutoffTime)
                .OrderByDescending(a => a.Timestamp)
                .ToListAsync();
        }

        public async Task<int> GetAlertCountInTimeRange(string sourceIp, TimeSpan timeWindow)
        {
            var cutoffTime = DateTime.UtcNow.Subtract(timeWindow);
            return await _context.Alerts
                .CountAsync(a => a.SourceIp == sourceIp && a.Timestamp >= cutoffTime);
        }

        public async Task<Dictionary<string, int>> GetAlertTypeFrequency(string sourceIp, TimeSpan timeWindow)
        {
            var cutoffTime = DateTime.UtcNow.Subtract(timeWindow);
            return await _context.Alerts
                .Where(a => a.SourceIp == sourceIp && a.Timestamp >= cutoffTime)
                .GroupBy(a => a.AlertType.Name)
                .ToDictionaryAsync(g => g.Key, g => g.Count());
        }

        public async Task<IEnumerable<Alert>> GetAlertsBySeverityAsync(int severityLevelId)
        {
            return await _context.Alerts
                .Where(a => a.SeverityLevelId == severityLevelId)
                .OrderByDescending(a => a.Timestamp)
                .ToListAsync();
        }

        public async Task<IEnumerable<Alert>> GetAlertsByStatusAsync(int statusId)
        {
            return await _context.Alerts
                .Where(a => a.StatusId == statusId)
                .OrderByDescending(a => a.Timestamp)
                .ToListAsync();
        }

        public async Task<Alert?> AssignAlertAsync(int alertId, string userId)
        {
            var alert = await _context.Alerts.FindAsync(alertId);
            if (alert == null) return null;

            alert.AssignedToId = userId;
            alert.StatusId = 2; // Assigned status

            await _context.SaveChangesAsync();
            return alert;
        }

        public async Task<Alert?> ResolveAlertAsync(int alertId, string userId, string resolution)
        {
            var alert = await _context.Alerts.FindAsync(alertId);
            if (alert == null) return null;

            alert.Resolution = resolution;
            alert.ResolvedById = userId;
            alert.ResolvedAt = DateTime.UtcNow;
            alert.StatusId = 3; // Resolved status

            await _context.SaveChangesAsync();
            return alert;
        }

        public async Task<IEnumerable<Alert>> GetCorrelatedAlertsAsync(Alert alert, TimeSpan window)
        {
            var cutoffTime = alert.Timestamp.Subtract(window);
            return await _context.Alerts
                .Where(a => a.Id != alert.Id &&
                           a.SourceIp == alert.SourceIp &&
                           a.Timestamp >= cutoffTime &&
                           a.Timestamp <= alert.Timestamp.Add(window))
                .OrderByDescending(a => a.Timestamp)
                .ToListAsync();
        }

        public async Task<IEnumerable<Alert>> GetAlertsByIpRangeAsync(string ipPrefix, TimeSpan window)
        {
            var cutoffTime = DateTime.UtcNow.Subtract(window);
            return await _context.Alerts
                .Where(a => a.SourceIp != null && a.SourceIp.StartsWith(ipPrefix) && a.Timestamp >= cutoffTime)
                .OrderByDescending(a => a.Timestamp)
                .ToListAsync();
        }

        public async Task<Dictionary<string, int>> GetAlertStatisticsAsync(TimeSpan window)
        {
            var cutoffTime = DateTime.UtcNow.Subtract(window);
            var stats = await _context.Alerts
                .Where(a => a.Timestamp >= cutoffTime)
                .GroupBy(a => a.AlertType.Name)
                .ToDictionaryAsync(g => g.Key, g => g.Count());

            return stats;
        }

        public async Task<Dictionary<DateTime, int>> GetAlertTrendAsync(TimeSpan window, TimeSpan interval)
        {
            var cutoffTime = DateTime.UtcNow.Subtract(window);
            var alerts = await _context.Alerts
                .Where(a => a.Timestamp >= cutoffTime)
                .OrderBy(a => a.Timestamp)
                .ToListAsync();

            var trend = new Dictionary<DateTime, int>();
            var currentTime = cutoffTime;
            while (currentTime <= DateTime.UtcNow)
            {
                var count = alerts.Count(a => a.Timestamp >= currentTime && a.Timestamp < currentTime.Add(interval));
                trend[currentTime] = count;
                currentTime = currentTime.Add(interval);
            }

            return trend;
        }

        public async Task<Dictionary<string, double>> GetThreatScoreBySourceAsync(TimeSpan window)
        {
            var cutoffTime = DateTime.UtcNow.Subtract(window);
            var alerts = await _context.Alerts
                .Where(a => a.Timestamp >= cutoffTime)
                .Include(a => a.SeverityLevel)
                .ToListAsync();

            return alerts
                .GroupBy(a => a.SourceIp ?? "unknown")
                .ToDictionary(
                    g => g.Key,
                    g => g.Average(a => a.SeverityLevel.Priority * (1 + Math.Log10(g.Count())))
                );
        }

        public async Task<Dictionary<string, double>> GetThreatScoreByTargetAsync(TimeSpan window)
        {
            var cutoffTime = DateTime.UtcNow.Subtract(window);
            var alerts = await _context.Alerts
                .Where(a => a.Timestamp >= cutoffTime && a.TargetIp != null)
                .Include(a => a.SeverityLevel)
                .ToListAsync();

            return alerts
                .GroupBy(a => a.TargetIp ?? "unknown")
                .ToDictionary(
                    g => g.Key,
                    g => g.Average(a => a.SeverityLevel.Priority * (1 + Math.Log10(g.Count())))
                );
        }

        public async Task<AlertStatus?> GetAlertStatusByNameAsync(string statusName)
        {
            return await _context.AlertStatuses
                .FirstOrDefaultAsync(s => s.Name == statusName);
        }

        public async Task<IEnumerable<AlertType>> GetAllAlertTypesAsync()
        {
            return await _context.AlertTypes.ToListAsync();
        }

        public async Task<IEnumerable<SeverityLevel>> GetAllSeverityLevelsAsync()
        {
            return await _context.SeverityLevels.ToListAsync();
        }
    }
}
