using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using SecurityMonitor.Extensions;
using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SecurityMonitor.Middleware
{
    public class DetailedRequestLoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<DetailedRequestLoggingMiddleware> _logger;

        public DetailedRequestLoggingMiddleware(RequestDelegate next, ILogger<DetailedRequestLoggingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Lấy và lưu IP vào context.Items ngay từ đầu
            var clientIP = context.GetClientIP();
            context.Items["ClientIP"] = clientIP;

            var sb = new StringBuilder();
            sb.AppendLine("=== Detailed Request Information ===");
            sb.AppendLine($"Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
            sb.AppendLine($"Request Method: {context.Request.Method}");
            sb.AppendLine($"Request Path: {context.Request.Path}");
            
            // Log các nguồn IP khác nhau để debug
            sb.AppendLine($"X-Forwarded-For: {context.Request.Headers["X-Forwarded-For"].ToString()}");
            sb.AppendLine($"X-Real-IP: {context.Request.Headers["X-Real-IP"].ToString()}");
            sb.AppendLine($"RemoteIpAddress: {context.Connection.RemoteIpAddress}");
            sb.AppendLine($"Client Port: {context.Connection.RemotePort}");
            sb.AppendLine($"IsHttps: {context.Request.IsHttps}");
            sb.AppendLine($"Scheme: {context.Request.Scheme}");
            sb.AppendLine($"Host: {context.Request.Host}");
            
            // Sử dụng extension method để lấy IP thật
            var realIp = context.GetClientIP();
            sb.AppendLine($"Real Client IP: {realIp}");
            
            sb.AppendLine("\n=== Request Headers ===");
            foreach (var header in context.Request.Headers.OrderBy(h => h.Key))
            {
                sb.AppendLine($"{header.Key}: {header.Value}");
            }

            _logger.LogInformation(sb.ToString());

            try 
            {
                await _next(context);
            }
            finally 
            {
                var responseInfo = new StringBuilder();
                responseInfo.AppendLine("\n=== Response Information ===");
                responseInfo.AppendLine($"Status Code: {context.Response.StatusCode}");
                responseInfo.AppendLine($"Content-Type: {context.Response.ContentType}");
                
                responseInfo.AppendLine("\n=== Response Headers ===");
                foreach (var header in context.Response.Headers.OrderBy(h => h.Key))
                {
                    responseInfo.AppendLine($"{header.Key}: {header.Value}");
                }
                
                _logger.LogInformation(responseInfo.ToString());
            }
        }
    }
}
