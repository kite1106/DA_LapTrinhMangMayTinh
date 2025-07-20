using SecurityMonitor.Models;

namespace SecurityMonitor.Services.Interfaces
{
    public interface IAlertService
    {
        Task<IEnumerable<Alert>> GetAllAlertsAsync();
        Task<Alert?> GetAlertByIdAsync(int id);
        Task<Alert> CreateAlertAsync(Alert alert);
        Task<Alert?> UpdateAlertAsync(int id, Alert alert);
        Task<bool> DeleteAlertAsync(int id);
        Task<List<Alert>> GetRecentAlertsBySourceIp(string sourceIp, TimeSpan timeWindow);
        Task<int> GetAlertCountInTimeRange(string sourceIp, TimeSpan timeWindow);
        Task<Dictionary<string, int>> GetAlertTypeFrequency(string sourceIp, TimeSpan timeWindow);
        Task<IEnumerable<Alert>> GetAlertsBySeverityAsync(int severityLevelId);
        Task<IEnumerable<Alert>> GetAlertsByStatusAsync(int statusId);
        Task<Alert?> AssignAlertAsync(int alertId, string userId);
        Task<Alert?> ResolveAlertAsync(int alertId, string userId, string resolution);
        
        // Phân tích tương quan và xu hướng
        Task<IEnumerable<Alert>> GetCorrelatedAlertsAsync(Alert alert, TimeSpan window);
        Task<IEnumerable<Alert>> GetAlertsByIpRangeAsync(string ipPrefix, TimeSpan window);
        
        // Phân tích thống kê
        Task<Dictionary<string, int>> GetAlertStatisticsAsync(TimeSpan window);
        Task<Dictionary<DateTime, int>> GetAlertTrendAsync(TimeSpan window, TimeSpan interval);
        
        // Phân tích mối đe dọa
        Task<Dictionary<string, double>> GetThreatScoreBySourceAsync(TimeSpan window);
        Task<Dictionary<string, double>> GetThreatScoreByTargetAsync(TimeSpan window);

        // Lấy trạng thái cảnh báo theo tên
        Task<AlertStatus?> GetAlertStatusByNameAsync(string statusName);
    }
}
