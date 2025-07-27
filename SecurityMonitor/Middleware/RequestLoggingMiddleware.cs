using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace SecurityMonitor.Middleware
{
    public class RequestLoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<RequestLoggingMiddleware> _logger;

        public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Log tất cả headers trước khi xử lý
            LogHeaders(context, "BEFORE ForwardedHeaders middleware");
            
            // Log địa chỉ IP
            _logger.LogDebug($"Connection.RemoteIpAddress: {context.Connection.RemoteIpAddress}");
            
            await _next(context);
            
            // Log sau khi đã xử lý
            LogHeaders(context, "AFTER ForwardedHeaders middleware");
            _logger.LogDebug($"Final RemoteIpAddress: {context.Connection.RemoteIpAddress}");
        }

        private void LogHeaders(HttpContext context, string stage)
        {
            _logger.LogDebug($"=== Request Headers at {stage} ===");
            foreach (var header in context.Request.Headers)
            {
                _logger.LogDebug($"{header.Key}: {header.Value}");
            }
            _logger.LogDebug("=== End Headers ===");
        }
    }
}
