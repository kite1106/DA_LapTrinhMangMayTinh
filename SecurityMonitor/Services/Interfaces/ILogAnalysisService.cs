using SecurityMonitor.Models;

namespace SecurityMonitor.Services.Interfaces
{
    public interface ILogAnalysisService
    {
        /// <summary>
        /// Phân tích logs gần đây để tìm các pattern đáng ngờ và tạo cảnh báo
        /// </summary>
        Task AnalyzeRecentLogsAndCreateAlertsAsync();
        
        /// <summary>
        /// Kiểm tra xem có nên phân tích logs không (dựa trên thời gian)
        /// </summary>
        bool ShouldAnalyzeLogs();
    }
} 