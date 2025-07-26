using Microsoft.AspNetCore.Identity;
using System.Net;
using System.Security.Claims;
using SecurityMonitor.Models;
using SecurityMonitor.Services;
using SecurityMonitor.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using SecurityMonitor.Extensions;
using SecurityMonitor.Services.Implementation;
using Microsoft.Extensions.DependencyInjection;
using SecurityMonitor.Services.Interfaces;

namespace SecurityMonitor.Middleware
{
    public class LoginMonitorMiddleware
    {

        private readonly RequestDelegate _next;
        private readonly ILogger<LoginMonitorMiddleware> _logger;

        private string GetRealPublicIP(string ipAddress)
        {
            // Kiểm tra IP rỗng
            if (string.IsNullOrEmpty(ipAddress))
                return "unknown";

            // Parse IP address
            if (IPAddress.TryParse(ipAddress, out var parsedIP))
            {
                // Chuyển IPv6 về IPv4 nếu có thể
                if (parsedIP.IsIPv4MappedToIPv6)
                    parsedIP = parsedIP.MapToIPv4();
                
                return parsedIP.ToString();
            }
            return "unknown";
        }

        private bool IsPrivateIP(string ipAddress)
        {
            if (string.IsNullOrEmpty(ipAddress) || ipAddress == "unknown" || ipAddress == "localhost" || ipAddress == "::1" || ipAddress == "127.0.0.1")
                return true;

            if (IPAddress.TryParse(ipAddress, out var parsedIP))
            {
                var bytes = parsedIP.GetAddressBytes();
                if (parsedIP.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                {
                    // Check for IPv4 private ranges
                    return bytes[0] == 10 || // 10.x.x.x
                           (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) || // 172.16.x.x - 172.31.x.x
                           (bytes[0] == 192 && bytes[1] == 168); // 192.168.x.x
                }
                else if (parsedIP.IsIPv6LinkLocal || parsedIP.IsIPv6SiteLocal)
                {
                    return true;
                }
            }
            return false;
        }

        public LoginMonitorMiddleware(RequestDelegate next, ILogger<LoginMonitorMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        private string GetClientIpAddress(HttpContext context)
        {
            // Debug tất cả các headers
            foreach (var header in context.Request.Headers)
            {
                _logger.LogInformation("🔍 Header {Key}: {Value}", header.Key, header.Value);
            }

            // Thử lấy IP từ các header khác nhau theo thứ tự ưu tiên
            string? ip = null;

            // 1. Thử X-Real-IP trước (thường được set bởi nginx)
            if (context.Request.Headers.TryGetValue("X-Real-IP", out var realIp))
            {
                ip = realIp.ToString();
                _logger.LogInformation("📍 Found X-Real-IP: {IP}", ip);
            }

            // 2. Thử X-Forwarded-For nếu không có X-Real-IP
            if (string.IsNullOrEmpty(ip) && context.Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor))
            {
                ip = forwardedFor.ToString().Split(',')[0].Trim();
                _logger.LogInformation("📍 Found X-Forwarded-For: {IP}", ip);
            }

            // 3. Cuối cùng dùng RemoteIpAddress
            if (string.IsNullOrEmpty(ip) && context.Connection.RemoteIpAddress != null)
            {
                ip = context.Connection.RemoteIpAddress.ToString();
                if (context.Connection.RemoteIpAddress.IsIPv4MappedToIPv6)
                {
                    ip = context.Connection.RemoteIpAddress.MapToIPv4().ToString();
                }
                _logger.LogInformation("📍 Using RemoteIpAddress: {IP}", ip);
            }

            // Kiểm tra IP có phải private/localhost không
            if (!string.IsNullOrEmpty(ip))
            {
                if (ip == "::1" || ip == "127.0.0.1" || ip.ToLower() == "localhost")
                {
                    _logger.LogWarning("⚠️ Got localhost IP, trying to find real IP...");
                    // Thử tìm IP thật từ các header khác
                    if (context.Request.Headers.TryGetValue("X-Forwarded-Host", out var host))
                    {
                        var possibleIp = host.ToString().Split(':')[0];
                        if (IPAddress.TryParse(possibleIp, out _))
                        {
                            ip = possibleIp;
                            _logger.LogInformation("📍 Using IP from X-Forwarded-Host: {IP}", ip);
                        }
                    }
                }
                else if (IsPrivateIP(ip))
                {
                    _logger.LogWarning("⚠️ Got private IP: {IP}, trying to find public IP...", ip);
                }
            }

            if (string.IsNullOrEmpty(ip))
            {
                _logger.LogWarning("⚠️ Could not determine client IP!");
                ip = "unknown";
            }

            _logger.LogInformation("✅ Final determined IP: {IP}", ip);
            return ip;
        }

        public async Task InvokeAsync(
            HttpContext context,
            SignInManager<ApplicationUser> signInManager,
            IServiceProvider serviceProvider)
        {
            // 1. Lấy IP của client khi vừa vào web
            var clientIP = GetClientIpAddress(context);
            _logger.LogInformation("⭐ Có người truy cập với IP: {IP}", clientIP);
            _logger.LogInformation("📍 Đường dẫn truy cập: {Path}", context.Request.Path);

            // Kiểm tra IP có bị block không
            using (var scope = serviceProvider.CreateScope())
            {
                var ipBlockingService = scope.ServiceProvider.GetRequiredService<IIPBlockingService>();
                if (await ipBlockingService.IsIPBlockedAsync(clientIP))
                {
                    _logger.LogWarning("🚫 IP bị chặn đang cố truy cập: {IP}", clientIP);
                    context.Response.StatusCode = 403;
                    await context.Response.WriteAsync("Access denied - Your IP has been blocked.");
                    return;
                }
            }

            // 2. Lưu IP để dùng sau
            context.Items["ClientIP"] = clientIP;

            // 3. Kiểm tra có phải request login không
            var wasAuthenticated = context.User?.Identity?.IsAuthenticated ?? false;
            
            // 4. Xử lý request
            await _next(context);

            // Kiểm tra nếu là request đăng nhập thành công
            if (!wasAuthenticated && context.User?.Identity?.IsAuthenticated == true)
            {
                var ipAddress = context.Items["ClientIP"]?.ToString() ?? GetClientIpAddress(context);
                var username = context.User.Identity?.Name ?? "unknown";

                // Lấy service để kiểm tra IP
                using var scope = serviceProvider.CreateScope();
                var auditService = scope.ServiceProvider.GetRequiredService<IAuditService>();
                var alertService = scope.ServiceProvider.GetRequiredService<IAlertService>();
                var ipCheckerService = scope.ServiceProvider.GetRequiredService<IIPCheckerService>();

                // Log audit
                var userId = context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                await auditService.LogActivityAsync(
                    userId: userId ?? "anonymous",
                    action: "Login Success",
                    entityType: "Authentication",
                    entityId: username,
                    details: $"User {username} logged in from {ipAddress}",
                    ipAddress: ipAddress);

                _logger.LogInformation("👤 Người dùng {Username} đăng nhập từ IP {IP}", username, ipAddress);

                // Kiểm tra IP có đáng ngờ không
                var alerts = await ipCheckerService.CheckIPAsync(ipAddress);

                if (alerts.Any())
                {
                    foreach (var alert in alerts)
                    {
                        // Thêm thông tin về user đăng nhập vào cảnh báo
                        alert.Description += $"\nNgười dùng: {username}";
                        await alertService.CreateAlertAsync(alert);

                        _logger.LogWarning("🚨 Phát hiện IP đáng ngờ: {IP} từ user {Username}", ipAddress, username);
                    }
                }
            }
        }
    }

}
