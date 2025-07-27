using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Diagnostics;

namespace SecurityMonitor.Middleware
{
    public class IPDebugMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<IPDebugMiddleware> _logger;

        public IPDebugMiddleware(RequestDelegate next, ILogger<IPDebugMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try 
            {
            var sb = new StringBuilder();
            sb.AppendLine("\n=== IP Debug Information ===");
            sb.AppendLine($"Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
            sb.AppendLine($"Path: {context.Request.Path}");
            sb.AppendLine($"Method: {context.Request.Method}");
            
            // Log Connection Info
            sb.AppendLine("\n=== Connection Info ===");
            sb.AppendLine($"RemoteIpAddress: {context.Connection.RemoteIpAddress}");
            sb.AppendLine($"RemotePort: {context.Connection.RemotePort}");
            sb.AppendLine($"LocalIpAddress: {context.Connection.LocalIpAddress}");
            sb.AppendLine($"LocalPort: {context.Connection.LocalPort}");
            
            // Log All Headers
            sb.AppendLine("\n=== All Request Headers ===");
            foreach (var header in context.Request.Headers.OrderBy(h => h.Key))
            {
                sb.AppendLine($"{header.Key}: {header.Value}");
            }
            
            // Log Important Headers
            sb.AppendLine("\n=== Important Headers ===");
            var importantHeaders = new[]
            {
                "X-Forwarded-For",
                "X-Real-IP",
                "X-Original-For",
                "CF-Connecting-IP",
                "X-Client-IP",
                "Forwarded"
            };

            foreach (var header in importantHeaders)
            {
                var value = context.Request.Headers[header].ToString();
                if (!string.IsNullOrEmpty(value))
                {
                    sb.AppendLine($"{header}: {value}");
                }
            }

            // Write to Windows Debug Output
            Debug.WriteLine(sb.ToString());
            _logger.LogInformation(sb.ToString());

            // Get Real IP
            var realIP = GetRealIPAddress(context);
            var finalDebug = new StringBuilder();
            finalDebug.AppendLine("\n=== Final IP Detection Results ===");
            finalDebug.AppendLine($"Detected Real IP: {realIP}");
            
            Debug.WriteLine(finalDebug.ToString());
            _logger.LogInformation(finalDebug.ToString());

            // Continue processing
            await _next(context);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in IPDebugMiddleware: {ex}");
                _logger.LogError(ex, "Error in IPDebugMiddleware");
                throw;
            }
        }

        private string GetRealIPAddress(HttpContext context)
        {
            // 1. Try X-Real-IP first (added by ngrok)
            var realIP = context.Request.Headers["X-Real-IP"].ToString();
            if (!string.IsNullOrEmpty(realIP))
            {
                _logger.LogInformation($"Found X-Real-IP: {realIP}");
                return realIP;
            }

            // 2. Try X-Forwarded-For
            var forwardedFor = context.Request.Headers["X-Forwarded-For"].ToString();
            if (!string.IsNullOrEmpty(forwardedFor))
            {
                _logger.LogInformation($"Found X-Forwarded-For: {forwardedFor}");
                var firstIP = forwardedFor.Split(',').FirstOrDefault()?.Trim();
                if (!string.IsNullOrEmpty(firstIP))
                {
                    return firstIP;
                }
            }

            // 3. Fall back to RemoteIpAddress
            var remoteIP = context.Connection.RemoteIpAddress;
            if (remoteIP != null)
            {
                if (remoteIP.IsIPv4MappedToIPv6)
                {
                    remoteIP = remoteIP.MapToIPv4();
                }
                
                return remoteIP.ToString();
            }

            return "unknown";
        }
    }
}
