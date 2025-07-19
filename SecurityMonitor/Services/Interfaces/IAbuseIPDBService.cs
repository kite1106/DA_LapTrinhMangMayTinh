using SecurityMonitor.Models;

namespace SecurityMonitor.Services.Interfaces
{
    public interface IAbuseIPDBService
    {
        Task<IEnumerable<Alert>> CheckIPAsync(string ipAddress);
        Task<Alert> ReportIPAsync(string ipAddress, string categories, string comment);
        Task<IEnumerable<Alert>> GetBlacklistedIPsAsync();
        Task<Dictionary<string, int>> GetReportStatisticsAsync(TimeSpan window);
        Task<Dictionary<string, double>> GetConfidenceScoresAsync(IEnumerable<string> ipAddresses);
    }
}
