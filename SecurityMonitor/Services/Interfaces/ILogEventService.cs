using SecurityMonitor.Models;

namespace SecurityMonitor.Services.Interfaces;

public interface ILogEventService
{
    // Theo dõi các sự kiện liên quan đến API
    Task RecordApiEventAsync(string endpoint, string method, string userId, string ipAddress, int statusCode);
    
    // Theo dõi các sự kiện đăng nhập/xác thực
    Task RecordAuthEventAsync(string action, string userId, string ipAddress, bool isSuccessful);
    
    // Theo dõi các sự kiện hệ thống (errors, warnings, etc)
    Task RecordSystemEventAsync(string eventType, string message, string source, string? ipAddress = null);
    
    // Theo dõi các hành vi đáng ngờ
    Task RecordSuspiciousEventAsync(string eventType, string description, string ipAddress, string? userId = null);
}
