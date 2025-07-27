using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Text;

namespace SecurityMonitor.Middleware
{
    public class ClientIPDebugMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ClientIPDebugMiddleware> _logger;

        public ClientIPDebugMiddleware(RequestDelegate next, ILogger<ClientIPDebugMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== Client IP Debug Information ===");
            sb.AppendLine($"Time: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}");
            sb.AppendLine($"Path: {context.Request.Path}");
            sb.AppendLine($"RemoteIpAddress: {context.Connection.RemoteIpAddress}");
            
            // Log all X-Forwarded-* headers
            var forwardedFor = context.Request.Headers["X-Forwarded-For"].ToString();
            var forwardedProto = context.Request.Headers["X-Forwarded-Proto"].ToString();
            var forwardedHost = context.Request.Headers["X-Forwarded-Host"].ToString();
            
            sb.AppendLine($"X-Forwarded-For: {forwardedFor}");
            sb.AppendLine($"X-Forwarded-Proto: {forwardedProto}");
            sb.AppendLine($"X-Forwarded-Host: {forwardedHost}");
            
            // Log other common proxy headers
            sb.AppendLine($"X-Real-IP: {context.Request.Headers["X-Real-IP"]}");
            sb.AppendLine($"True-Client-IP: {context.Request.Headers["True-Client-IP"]}");
            sb.AppendLine($"CF-Connecting-IP: {context.Request.Headers["CF-Connecting-IP"]}");

            _logger.LogInformation(sb.ToString());

            // Get and store the real IP in the context
            string clientIP = GetRealClientIP(context);
            context.Items["ClientIP"] = clientIP;
            _logger.LogInformation($"Stored client IP in context: {clientIP}");

            await _next(context);
        }

        private string GetRealClientIP(HttpContext context)
        {
            // Try X-Forwarded-For first
            var forwardedFor = context.Request.Headers["X-Forwarded-For"].ToString();
            if (!string.IsNullOrEmpty(forwardedFor))
            {
                var ip = forwardedFor.Split(',')[0].Trim();
                if (!string.IsNullOrEmpty(ip))
                {
                    _logger.LogInformation($"Using IP from X-Forwarded-For: {ip}");
                    return ip;
                }
            }

            // Try X-Real-IP
            var realIP = context.Request.Headers["X-Real-IP"].ToString();
            if (!string.IsNullOrEmpty(realIP))
            {
                _logger.LogInformation($"Using IP from X-Real-IP: {realIP}");
                return realIP;
            }

            // Fall back to RemoteIpAddress
            var remoteIP = context.Connection.RemoteIpAddress;
            if (remoteIP != null)
            {
                if (remoteIP.IsIPv4MappedToIPv6)
                {
                    remoteIP = remoteIP.MapToIPv4();
                }
                var ip = remoteIP.ToString();
                _logger.LogInformation($"Using RemoteIpAddress: {ip}");
                return ip;
            }

            _logger.LogWarning("Could not determine client IP");
            return "unknown";
        }
    }
}
