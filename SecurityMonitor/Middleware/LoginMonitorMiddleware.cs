using Microsoft.AspNetCore.Identity;
using System.Net;
using System.Security.Claims;
using SecurityMonitor.Models;
using SecurityMonitor.Services;
using SecurityMonitor.Services.Interfaces;
using Microsoft.AspNetCore.Http;

namespace SecurityMonitor.Middleware;

public static class HttpContextExtensions
{
    public static string GetRealIPAddress(this HttpContext context)
    {
            // Log tất cả các headers để debug
            foreach (var header in context.Request.Headers)
            {
                Console.WriteLine($"Header: {header.Key} = {header.Value}");
            }

            // Try to get IP from headers in order of preference
            string? ip = null;
            
            // 1. Try X-Forwarded-For
            if (context.Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor))
            {
                ip = forwardedFor.ToString().Split(',')[0].Trim();
                Console.WriteLine($"Found X-Forwarded-For IP: {ip}");
            }
            
            // 2. Try X-Real-IP
            if (string.IsNullOrEmpty(ip) && context.Request.Headers.TryGetValue("X-Real-IP", out var realIp))
            {
                ip = realIp.ToString().Trim();
                Console.WriteLine($"Found X-Real-IP: {ip}");
            }

            // 3. Try RemoteIpAddress as last resort
            if (string.IsNullOrEmpty(ip))
            {
                ip = context.Connection.RemoteIpAddress?.ToString();
                Console.WriteLine($"Using RemoteIpAddress: {ip}");
            }

            // Validate and normalize IP
            if (!string.IsNullOrEmpty(ip) && IPAddress.TryParse(ip, out var parsedIP))
            {
                // Convert IPv4-mapped IPv6 addresses to IPv4
                if (parsedIP.IsIPv4MappedToIPv6)
                {
                    var ipv4 = parsedIP.MapToIPv4().ToString();
                    Console.WriteLine($"Converted IPv6-mapped IPv4: {ipv4}");
                    return ipv4;
                }

                Console.WriteLine($"Using valid IP: {ip}");
                return ip;
            }

            Console.WriteLine("No valid IP found, returning unknown");
            return "unknown";


        }
    }

    public class LoginMonitorMiddleware
    {

    private readonly RequestDelegate _next;
    private readonly ILogger<LoginMonitorMiddleware> _logger;

    private string GetRealPublicIP(string ipAddress)
    {
        // Nếu là localhost hoặc IP private, trả về rỗng
        if (IsPrivateIP(ipAddress))
            return string.Empty;
        return ipAddress;
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
        _logger.LogInformation("Getting client IP address...");
        
        var ip = context.GetRealIPAddress();
        _logger.LogInformation("Raw IP from GetRealIPAddress: {IP}", ip);
        
        var publicIP = GetRealPublicIP(ip);
        _logger.LogInformation("Public IP after filtering: {IP}", publicIP);
        
        return publicIP;
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
