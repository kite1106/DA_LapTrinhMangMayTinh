using Microsoft.EntityFrameworkCore;
using SecurityMonitor.Models;

namespace SecurityMonitor.Data;

public static class SeedData
{
    public static void Initialize(IServiceProvider serviceProvider)
    {
        using (var context = new ApplicationDbContext(
            serviceProvider.GetRequiredService<DbContextOptions<ApplicationDbContext>>()))
        {
            // Thêm AlertTypes
            if (!context.AlertTypes.Any())
            {
                context.AlertTypes.AddRange(
                    new AlertType { Id = (int)AlertTypeId.SQLInjection, Name = "SQL Injection", Category = "Attack", Description = "Tấn công chèn mã SQL", CreatedAt = DateTime.Now },
                    new AlertType { Id = (int)AlertTypeId.BruteForce, Name = "Brute Force", Category = "Attack", Description = "Tấn công dò mật khẩu", CreatedAt = DateTime.Now },
                    new AlertType { Id = (int)AlertTypeId.DDoS, Name = "DDoS", Category = "Attack", Description = "Tấn công từ chối dịch vụ phân tán", CreatedAt = DateTime.Now },
                    new AlertType { Id = (int)AlertTypeId.Malware, Name = "Malware", Category = "Malicious", Description = "Phần mềm độc hại", CreatedAt = DateTime.Now },
                    new AlertType { Id = (int)AlertTypeId.DataLeak, Name = "Data Leak", Category = "Data", Description = "Rò rỉ dữ liệu", CreatedAt = DateTime.Now },
                    new AlertType { Id = (int)AlertTypeId.SuspiciousIP, Name = "Suspicious IP", Category = "IP", Description = "IP đáng ngờ", CreatedAt = DateTime.Now },
                    new AlertType { Id = (int)AlertTypeId.ReportedIP, Name = "Reported IP", Category = "IP", Description = "IP bị báo cáo", CreatedAt = DateTime.Now },
                    new AlertType { Id = (int)AlertTypeId.BlacklistedIP, Name = "Blacklisted IP", Category = "IP", Description = "IP trong danh sách đen", CreatedAt = DateTime.Now }
                );
            }

            // Thêm SeverityLevels
            if (!context.SeverityLevels.Any())
            {
                context.SeverityLevels.AddRange(
                    new SeverityLevel { Id = (int)SeverityLevelId.Low, Name = "Low", Description = "Mức độ thấp", Color = "#28a745", Priority = 1 },
                    new SeverityLevel { Id = (int)SeverityLevelId.Medium, Name = "Medium", Description = "Mức độ trung bình", Color = "#ffc107", Priority = 2 },
                    new SeverityLevel { Id = (int)SeverityLevelId.High, Name = "High", Description = "Mức độ cao", Color = "#dc3545", Priority = 3 },
                    new SeverityLevel { Id = (int)SeverityLevelId.Critical, Name = "Critical", Description = "Mức độ nghiêm trọng", Color = "#6610f2", Priority = 4 }
                );
            }

            // Thêm AlertStatuses
            if (!context.AlertStatuses.Any())
            {
                context.AlertStatuses.AddRange(
                    new AlertStatus { Id = (int)AlertStatusId.New, Name = "New", Description = "Cảnh báo mới", Color = "#17a2b8", IsTerminal = false },
                    new AlertStatus { Id = (int)AlertStatusId.InProgress, Name = "In Progress", Description = "Đang xử lý", Color = "#007bff", IsTerminal = false },
                    new AlertStatus { Id = (int)AlertStatusId.Resolved, Name = "Resolved", Description = "Đã xử lý", Color = "#28a745", IsTerminal = true },
                    new AlertStatus { Id = (int)AlertStatusId.FalsePositive, Name = "False Positive", Description = "Cảnh báo sai", Color = "#6c757d", IsTerminal = true },
                    new AlertStatus { Id = (int)AlertStatusId.Ignored, Name = "Ignored", Description = "Đã bỏ qua", Color = "#6c757d", IsTerminal = true }
                );
            }

            // Thêm LogLevelTypes
            if (!context.LogLevelTypes.Any())
            {
                context.LogLevelTypes.AddRange(
                    new LogLevelType { Name = "Information", Description = "Thông tin", Priority = 1 },
                    new LogLevelType { Name = "Warning", Description = "Cảnh báo", Priority = 2 },
                    new LogLevelType { Name = "Error", Description = "Lỗi", Priority = 3 },
                    new LogLevelType { Name = "Critical", Description = "Nghiêm trọng", Priority = 4 },
                    new LogLevelType { Name = "Debug", Description = "Debug", Priority = 5 }
                );
            }

            // Thêm LogSources mẫu
            if (!context.LogSources.Any())
            {
                context.LogSources.AddRange(
                    new LogSource 
                    { 
                        Name = "Windows Server 2022", 
                        DeviceType = "Windows Server",
                        IpAddress = "20.168.122.88",
                        Location = "Ho Chi Minh",
                        IsActive = true,
                        LastSeenAt = DateTime.Now
                    },
                    new LogSource 
                    { 
                        Name = "Ubuntu Server", 
                        DeviceType = "Linux Server",
                        IpAddress = "88.169.229.95",
                        Location = "Ha Noi",
                        IsActive = true,
                        LastSeenAt = DateTime.Now
                    },
                    new LogSource 
                    { 
                        Name = "Database Server", 
                        DeviceType = "SQL Server",
                        IpAddress = "43.132.174.14",
                        Location = "Da Nang",
                        IsActive = true,
                        LastSeenAt = DateTime.Now
                    }
                );
            }

            // Lưu thay đổi để có Id của LogSources và LogLevelTypes
            context.SaveChanges();

            // Thêm Logs mẫu
                    if (!context.LogEntries.Any())
        {
            var winServer = context.LogSources.First(ls => ls.Name == "Windows Server 2022");
            var ubuntuServer = context.LogSources.First(ls => ls.Name == "Ubuntu Server");
            var dbServer = context.LogSources.First(ls => ls.Name == "Database Server");

                // Lấy LogLevelType mặc định
                var infoLevel = context.LogLevelTypes.First(lt => lt.Name == "Information");
                var errorLevel = context.LogLevelTypes.First(lt => lt.Name == "Error");
                var warningLevel = context.LogLevelTypes.First(lt => lt.Name == "Warning");

                context.LogEntries.AddRange(
                    new LogEntry
                    {
                        Timestamp = DateTime.Now.AddMinutes(-30),
                        LogSourceId = winServer.Id,
                        Message = "Failed login attempt for user 'administrator'",
                        Details = "Event ID: 4625, Multiple failed login attempts detected",
                        IpAddress = "45.92.158.12",
                        ProcessedAt = DateTime.Now.AddMinutes(-29)
                    },
                    new LogEntry
                    {
                        Timestamp = DateTime.Now.AddMinutes(-25),
                        LogSourceId = ubuntuServer.Id,
                        Message = "Suspicious file access detected",
                        Details = "User 'root' accessed sensitive file /etc/passwd",
                        IpAddress = "189.23.45.66",
                        ProcessedAt = DateTime.Now.AddMinutes(-24)
                    },
                    new LogEntry
                    {
                        Timestamp = DateTime.Now.AddMinutes(-20),
                        LogSourceId = dbServer.Id,
                        Message = "SQL injection attempt detected",
                        Details = "Malicious SQL query: SELECT * FROM users WHERE id = 1 OR 1=1",
                        IpAddress = "78.45.123.90",
                        ProcessedAt = DateTime.Now.AddMinutes(-19)
                    },
                    new LogEntry
                    {
                        Timestamp = DateTime.Now.AddMinutes(-15),
                        LogSourceId = winServer.Id,
                        Message = "New service installed",
                        Details = "Service 'BadService' was installed and started",
                        ProcessedAt = DateTime.Now.AddMinutes(-14)
                    },
                    new LogEntry
                    {
                        Timestamp = DateTime.Now.AddMinutes(-10),
                        LogSourceId = ubuntuServer.Id,
                        Message = "Port scan detected",
                        Details = "Multiple connection attempts from IP 78.45.123.90",
                        IpAddress = "78.45.123.90",
                        ProcessedAt = DateTime.Now.AddMinutes(-9)
                    }
                );
            }

            // Thêm Alerts mẫu
            if (!context.Alerts.Any())
            {
                var randomUser = context.Users.FirstOrDefault(u => u.Email != "admin@securitymonitor.com");
                if (randomUser != null)
                {
                    context.Alerts.AddRange(
                        new Alert
                        {
                            Title = "Phát hiện tấn công SQL Injection",
                            Description = "Phát hiện chuỗi SQL độc hại",
                            AlertTypeId = (int)AlertTypeId.SQLInjection,
                            SeverityLevelId = (int)SeverityLevelId.High,
                            StatusId = (int)AlertStatusId.New,
                            SourceIp = "189.23.45.66",
                            Timestamp = DateTime.UtcNow.AddHours(-2),
                            AssignedToId = randomUser.Id
                        },
                        new Alert
                        {
                            Title = "Phát hiện nhiều lần đăng nhập thất bại",
                            Description = "Nhiều lần thử đăng nhập không thành công từ cùng một IP",
                            AlertTypeId = (int)AlertTypeId.BruteForce,
                            SeverityLevelId = (int)SeverityLevelId.Critical,
                            StatusId = (int)AlertStatusId.New,
                            SourceIp = "45.92.158.12",
                            Timestamp = DateTime.UtcNow.AddHours(-1),
                            AssignedToId = randomUser.Id
                        }
                    );
                }
            }

            // Thêm AuditLogs mẫu
            if (!context.AuditLogs.Any())
            {
                var users = context.Users.ToList();
                foreach (var user in users)
                {
                    context.AuditLogs.AddRange(
                        new AuditLog
                        {
                            UserId = user.Id,
                            Action = "Login",
                            Timestamp = DateTime.UtcNow.AddHours(-3),
                            IpAddress = "192.168.1.100",
                            EntityType = "Authentication",
                            EntityId = user.Id,
                            Details = "Đăng nhập thành công"
                        },
                        new AuditLog
                        {
                            UserId = user.Id,
                            Action = "PasswordChange",
                            Timestamp = DateTime.UtcNow.AddHours(-2),
                            IpAddress = "192.168.1.100",
                            EntityType = "Authentication",
                            EntityId = user.Id,
                            Details = "Đổi mật khẩu thành công"
                        },
                        new AuditLog
                        {
                            UserId = user.Id,
                            Action = "Login",
                            Timestamp = DateTime.UtcNow.AddMinutes(-30),
                            IpAddress = "192.168.1.100",
                            EntityType = "Authentication",
                            EntityId = user.Id,
                            Details = "Đăng nhập thành công"
                        }
                    );
                }
            }

            context.SaveChanges();
        }
    }
}
