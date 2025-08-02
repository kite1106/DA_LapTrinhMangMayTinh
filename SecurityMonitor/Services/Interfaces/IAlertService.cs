using SecurityMonitor.Models;

namespace SecurityMonitor.Services.Interfaces
{
    public interface IAlertService
    {
        Task<Alert> CreateAlertAsync(Alert alert);
        Task<Alert?> GetAlertByIdAsync(int id);
        Task UpdateAlertAsync(Alert alert);
        Task DeleteAlertAsync(int id);
        Task<int> GetAlertCountAsync();
        Task<IEnumerable<Alert>> GetRecentAlertsAsync(TimeSpan duration);
        Task<IEnumerable<Alert>> GetAllAlertsAsync();
        Task<bool> GetRecentAlertByIpAsync(string ip, TimeSpan timeWindow);
Task<bool> AlertExistsAsync(string ip, AlertTypeId alertTypeId);

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
        
        // Lấy danh sách các loại cảnh báo
        Task<IEnumerable<AlertType>> GetAllAlertTypesAsync();
        
        // Lấy danh sách các mức độ nghiêm trọng
        Task<IEnumerable<SeverityLevel>> GetAllSeverityLevelsAsync();
    }
}
