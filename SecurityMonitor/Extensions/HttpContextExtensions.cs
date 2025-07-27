using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Sockets;

namespace SecurityMonitor.Extensions
{
    public static class HttpContextExtensions
    {
        public static string GetClientIP(this HttpContext context)
        {
            if (context.Items.TryGetValue("ClientIP", out var clientIP))
            {
                return clientIP?.ToString() ?? "unknown";
            }
            return context.GetRealIPAddress();
        }

        public static string GetRealIPAddress(this HttpContext context)
        {
            // Lấy logger từ DI để log debug info
            var logger = context.RequestServices.GetService(typeof(ILogger<HttpContext>)) as ILogger<HttpContext>;

            // Thử lấy IP từ X-Forwarded-For header trước
            var forwardedFor = context.Request.Headers["X-Forwarded-For"].ToString();
            if (!string.IsNullOrEmpty(forwardedFor))
            {
                var ip = forwardedFor.Split(',')[0].Trim();
                if (IPAddress.TryParse(ip, out var parsedIP))
                {
                    logger?.LogInformation($"Using X-Forwarded-For IP: {ip}");
                    return ip;
                }
            }

            // Nếu không có X-Forwarded-For, dùng RemoteIpAddress
            var remoteIp = context.Connection.RemoteIpAddress;
            if (remoteIp != null)
            {
                // Chuyển IPv6 về IPv4 nếu có thể
                if (remoteIp.IsIPv4MappedToIPv6)
                    remoteIp = remoteIp.MapToIPv4();

                var ip = remoteIp.ToString();
                logger?.LogInformation($"Using RemoteIpAddress: {ip}");
                return ip;
            }

            logger?.LogWarning("Could not determine real IP address");
            return "unknown";
        }

        private static bool TryParseIPv4(string ipString, out string ipv4)
        {
            ipv4 = "unknown";

            if (IPAddress.TryParse(ipString, out var ip))
            {
                // Chuyển IPv6 thành IPv4 nếu có thể
                if (ip.IsIPv4MappedToIPv6)
                    ip = ip.MapToIPv4();

                // Chỉ chấp nhận IPv4
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    ipv4 = ip.ToString();
                    return true;
                }
            }

            return false;
        }

        private static bool IsPrivateIP(string ipAddress)
        {
            if (!IPAddress.TryParse(ipAddress, out var ip))
                return false;

            // Chuyển về IPv4 nếu là IPv6
            if (ip.IsIPv4MappedToIPv6)
                ip = ip.MapToIPv4();

            // Kiểm tra IPv6 private ranges
            if (ip.AddressFamily == AddressFamily.InterNetworkV6)
            {
                return ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal;
            }

            // Kiểm tra IPv4 private ranges
            var bytes = ip.GetAddressBytes();
            return bytes[0] == 10 || // 10.x.x.x
                   (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) || // 172.16.x.x to 172.31.x.x
                   (bytes[0] == 192 && bytes[1] == 168) || // 192.168.x.x
                   ipAddress == "127.0.0.1" || ipAddress == "::1" || ipAddress.ToLower() == "localhost";
        }
    }
}
