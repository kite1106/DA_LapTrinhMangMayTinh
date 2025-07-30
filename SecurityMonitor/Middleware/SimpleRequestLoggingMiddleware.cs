using Microsoft.AspNetCore.Identity;
using SecurityMonitor.Services.Interfaces;
using SecurityMonitor.Models;

namespace SecurityMonitor.Middleware
{
    public class SimpleRequestLoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<SimpleRequestLoggingMiddleware> _logger;

        public SimpleRequestLoggingMiddleware(RequestDelegate next, ILogger<SimpleRequestLoggingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, ILogEventService logEventService, UserManager<ApplicationUser> userManager)
        {
            var path = context.Request.Path.Value?.ToLower();
            var method = context.Request.Method;
            var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
            var userId = context.User?.Identity?.Name ?? "anonymous";

            // Log TẤT CẢ request để debug
            _logger.LogWarning("🔍 ALL REQUEST: {Method} {Path} from {UserId} at {IpAddress}", method, path, userId, ipAddress);

            // Nếu là POST request và có chứa "password" hoặc "identity"
            if (method == "POST" && (path?.Contains("password") == true || path?.Contains("identity") == true))
            {
                _logger.LogError("🚨 POTENTIAL PASSWORD CHANGE: {Method} {Path} from {UserId}", method, path, userId);
                
                // Gọi LogEventService để tạo alert
                await logEventService.RecordAuthEventAsync("ChangePassword", userId, ipAddress, true);
            }

            await _next(context);
        }
    }

    // Extension method để dễ dàng đăng ký middleware
    public static class SimpleRequestLoggingMiddlewareExtensions
    {
        public static IApplicationBuilder UseSimpleRequestLogging(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<SimpleRequestLoggingMiddleware>();
        }
    }
} 