using SecurityMonitor.Models;

namespace SecurityMonitor.Services.Interfaces
{
    public interface ILogAnalysisService
    {
        // Phân tích log entry
        Task<LogAnalysis> AnalyzeLogEntryAsync(LogEntry logEntry);
        
        // Phân tích batch logs
        Task<List<LogAnalysis>> AnalyzeLogEntriesAsync(List<LogEntry> logEntries);
        
        // Phân tích pattern
        Task<List<LogAnalysis>> AnalyzePatternAsync(string pattern, DateTime from, DateTime to);
        
        // Phát hiện anomaly
        Task<List<LogAnalysis>> DetectAnomaliesAsync(DateTime from, DateTime to);
        
        // Phân tích threat
        Task<List<LogAnalysis>> AnalyzeThreatsAsync(DateTime from, DateTime to);
        
        // Tạo alert từ phân tích
        Task<Alert?> CreateAlertFromAnalysisAsync(LogAnalysis analysis);
        
        // Lấy thống kê phân tích
        Task<object> GetAnalysisStatsAsync(DateTime from, DateTime to);
    }
} 