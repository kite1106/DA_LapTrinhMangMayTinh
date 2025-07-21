using Microsoft.AspNetCore.Identity;
using System.Net;
using System.Security.Claims;
using SecurityMonitor.Models;
using SecurityMonitor.Services;
using SecurityMonitor.Services.Interfaces;

namespace SecurityMonitor.Middleware;

public class LoginMonitorMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<LoginMonitorMiddleware> _logger;

    private string NormalizeIP(string ipAddress)
    {
        if (ipAddress == "::1" || ipAddress == "localhost")
            return "127.0.0.1";
        return ipAddress;
    }

    private bool IsPrivateIP(string ipAddress)
    {
        if (ipAddress == "unknown" || ipAddress == "localhost" || ipAddress == "::1" || ipAddress == "127.0.0.1")
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
        // Thử lấy IP từ các header của ngrok và các header phổ biến khác
        string? ip = context.Request.Headers["X-Forwarded-For"].FirstOrDefault() ??
                   context.Request.Headers["X-Real-IP"].FirstOrDefault() ??
                   context.Request.Headers["CF-Connecting-IP"].FirstOrDefault() ??
                   context.Request.Headers["X-Original-For"].FirstOrDefault() ??
                   context.Request.Headers["ngrok-trace-id"].FirstOrDefault();

        // Nếu không có trong header, lấy từ connection
        if (string.IsNullOrEmpty(ip))
        {
            var remoteIp = context.Connection.RemoteIpAddress;
            if (remoteIp != null)
            {
                // Nếu là IPv6, thử chuyển về IPv4 nếu có thể
                if (remoteIp.IsIPv4MappedToIPv6)
                {
                    remoteIp = remoteIp.MapToIPv4();
                }
                ip = remoteIp.ToString();
            }
        }

        // Nếu là localhost thì lấy IP local thực
        if (ip == "::1" || ip == "localhost" || string.IsNullOrEmpty(ip))
        {
            try
            {
                // Lấy tất cả các IP của máy local
                var hostName = System.Net.Dns.GetHostName();
                var addresses = System.Net.Dns.GetHostAddresses(hostName);
                
                // Tìm IPv4 không phải loopback
                var localIp = addresses.FirstOrDefault(a => 
                    a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork &&
                    !IPAddress.IsLoopback(a));

                if (localIp != null)
                {
                    return localIp.ToString();
                }
            }
            catch
            {
                // Nếu không lấy được IP local thì trả về 127.0.0.1
                return "127.0.0.1";
            }
        }

        return ip ?? "unknown";
    }

    public async Task InvokeAsync(
        HttpContext context,
        SignInManager<ApplicationUser> signInManager,
        IServiceProvider serviceProvider)
    {
        // Lưu trạng thái đăng nhập trước khi xử lý request
        bool wasAuthenticated = context.User?.Identity?.IsAuthenticated ?? false;

        // Xử lý request
        await _next(context);

        // Kiểm tra nếu là request đăng nhập thành công
        if (!wasAuthenticated && context.User?.Identity?.IsAuthenticated == true)
        {
            var ipAddress = GetClientIpAddress(context);
            var username = context.User.Identity?.Name ?? "unknown";

            // Lấy service để kiểm tra IP
            using var scope = serviceProvider.CreateScope();
            var ipChecker = scope.ServiceProvider.GetRequiredService<IIPCheckerService>();
            var alertService = scope.ServiceProvider.GetRequiredService<IAlertService>();
            var auditService = scope.ServiceProvider.GetRequiredService<IAuditService>();

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
            var alerts = await ipChecker.CheckIPAsync(ipAddress);
            
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
