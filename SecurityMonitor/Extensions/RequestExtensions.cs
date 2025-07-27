using Microsoft.AspNetCore.Http;
using System.Net;

namespace SecurityMonitor.Extensions
{
    public static class RequestExtensions
    {
        public static string GetRealIpAddress(this HttpContext context)
        {
            // Thứ tự ưu tiên để lấy IP:
            // 1. X-Real-IP header (thường được set bởi Nginx)
            // 2. X-Forwarded-For header đầu tiên (chuỗi IP, lấy IP đầu tiên)
            // 3. RemoteIpAddress từ connection

            string ip = null;

            // 1. Thử lấy từ X-Real-IP
            ip = context.Request.Headers["X-Real-IP"].FirstOrDefault();
            if (!string.IsNullOrEmpty(ip))
            {
                return ip;
            }

            // 2. Thử lấy từ X-Forwarded-For
            var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrEmpty(forwardedFor))
            {
                // X-Forwarded-For có thể chứa nhiều IP, lấy IP đầu tiên
                ip = forwardedFor.Split(',')[0].Trim();
                if (!string.IsNullOrEmpty(ip))
                {
                    return ip;
                }
            }

            // 3. Lấy từ connection
            if (context.Connection.RemoteIpAddress != null)
            {
                // Nếu là IPv6 localhost, chuyển thành IPv4 localhost
                if (IPAddress.IsLoopback(context.Connection.RemoteIpAddress))
                {
                    return "127.0.0.1";
                }

                // Nếu là IPv6, lấy mapping IPv4 nếu có
                if (context.Connection.RemoteIpAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
                {
                    var ipv4 = context.Connection.RemoteIpAddress.MapToIPv4();
                    return ipv4.ToString();
                }

                return context.Connection.RemoteIpAddress.ToString();
            }

            return "0.0.0.0"; // IP không xác định
        }

        public static bool IsLocalRequest(this HttpContext context)
        {
            var remoteIp = context.GetRealIpAddress();
            return remoteIp == "127.0.0.1" || remoteIp == "::1";
        }
    }
}
