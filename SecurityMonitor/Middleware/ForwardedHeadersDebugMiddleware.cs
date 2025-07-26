using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace SecurityMonitor.Middleware
{
    public class ForwardedHeadersDebugMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ForwardedHeadersDebugMiddleware> _logger;

        public ForwardedHeadersDebugMiddleware(RequestDelegate next, ILogger<ForwardedHeadersDebugMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            _logger.LogDebug("=== Request Details ===");
            _logger.LogDebug($"Scheme: {context.Request.Scheme}");
            _logger.LogDebug($"Host: {context.Request.Host}");
            _logger.LogDebug($"Path: {context.Request.Path}");
            _logger.LogDebug($"RemoteIpAddress: {context.Connection.RemoteIpAddress}");

            _logger.LogDebug("=== Request Headers ===");
            foreach (var header in context.Request.Headers)
            {
                _logger.LogDebug($"{header.Key}: {header.Value}");
            }

            await _next(context);
        }
    }
}
