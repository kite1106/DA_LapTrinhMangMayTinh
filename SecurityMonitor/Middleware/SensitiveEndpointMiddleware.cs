using SecurityMonitor.Services.Interfaces;

namespace SecurityMonitor.Middleware;

public class SensitiveEndpointMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<SensitiveEndpointMiddleware> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public SensitiveEndpointMiddleware(
        RequestDelegate next,
        ILogger<SensitiveEndpointMiddleware> logger,
        IServiceScopeFactory scopeFactory)
    {
        _next = next;
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Ghi lại thời điểm bắt đầu để tính thời gian xử lý
        var startTime = DateTime.UtcNow;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var logEventService = scope.ServiceProvider.GetRequiredService<ILogEventService>();

            // Ghi nhận request
            var path = context.Request.Path.Value?.ToLower() ?? "";
            var method = context.Request.Method;
            var userId = context.User?.Identity?.Name ?? "anonymous";
            var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            // Ghi log API event trước khi xử lý request
            await logEventService.RecordApiEventAsync(path, method, userId, ipAddress, context.Response.StatusCode);

            // Xử lý request
            await _next(context);

            // Nếu có lỗi, ghi nhận
            if (context.Response.StatusCode >= 400)
            {
                var responseTime = DateTime.UtcNow - startTime;
                var errorType = context.Response.StatusCode switch
                {
                    >= 500 => "ServerError",
                    >= 400 => "ClientError",
                    _ => "Unknown"
                };

                await logEventService.RecordSystemEventAsync(
                    errorType,
                    $"{method} {path} returned {context.Response.StatusCode} after {responseTime.TotalMilliseconds}ms",
                    "WebServer",
                    ipAddress
                );
            }
        }
        catch (Exception ex)
        {
            // Log lỗi không mong muốn
            using var scope = _scopeFactory.CreateScope();
            var logEventService = scope.ServiceProvider.GetRequiredService<ILogEventService>();
            await logEventService.RecordSystemEventAsync(
                "Error",
                ex.Message,
                "Middleware",
                context.Connection.RemoteIpAddress?.ToString()
            );
            throw; // Re-throw để error handling middleware xử lý
        }
    }
}
