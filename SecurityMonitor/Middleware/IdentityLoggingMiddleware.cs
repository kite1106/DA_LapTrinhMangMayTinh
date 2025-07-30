using Microsoft.AspNetCore.Identity;
using SecurityMonitor.Services.Interfaces;
using SecurityMonitor.Models;

namespace SecurityMonitor.Middleware
{
    public class IdentityLoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<IdentityLoggingMiddleware> _logger;

        public IdentityLoggingMiddleware(RequestDelegate next, ILogger<IdentityLoggingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, ILogEventService logEventService, UserManager<ApplicationUser> userManager)
        {
            var originalBodyStream = context.Response.Body;

            try
            {
                using var memoryStream = new MemoryStream();
                context.Response.Body = memoryStream;

                await _next(context);

                // Kiểm tra các hành động Identity
                await LogIdentityActions(context, logEventService, userManager);

                memoryStream.Position = 0;
                await memoryStream.CopyToAsync(originalBodyStream);
            }
            finally
            {
                context.Response.Body = originalBodyStream;
            }
        }

        private async Task LogIdentityActions(HttpContext context, ILogEventService logEventService, UserManager<ApplicationUser> userManager)
        {
            var path = context.Request.Path.Value?.ToLower();
            var method = context.Request.Method;
            var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
            var userId = context.User?.Identity?.Name ?? "anonymous";

            // Log TẤT CẢ request để debug
            _logger.LogInformation("🔍 REQUEST: {Method} {Path} from {UserId} at {IpAddress}", method, path, userId, ipAddress);

            // Kiểm tra tất cả request có chứa "identity", "manage", "changepassword", "password"
            if (path?.Contains("identity") == true || path?.Contains("manage") == true || 
                path?.Contains("changepassword") == true || path?.Contains("password") == true)
            {
                _logger.LogWarning("📝 Identity/Manage/Password request detected: {Method} {Path}", method, path);
            }

            // Bắt các hành động đổi mật khẩu - kiểm tra nhiều pattern
            if (method == "POST" && 
                (path?.Contains("/identity/account/manage/changepassword") == true ||
                 path?.Contains("/identity/account/changepassword") == true ||
                 path?.Contains("/changepassword") == true ||
                 path?.Contains("/manage/changepassword") == true ||
                 path?.Contains("changepassword") == true ||
                 path?.Contains("password") == true ||
                 path?.Contains("/identity/account/manage") == true))
            {
                _logger.LogWarning("🚨 PHÁT HIỆN ĐỔI MẬT KHẨU từ user: {UserId}", userId);
                await logEventService.RecordAuthEventAsync("ChangePassword", userId, ipAddress, true);
            }

            // Bắt các hành động reset password
            if (method == "POST" && 
                (path?.Contains("/identity/account/resetpassword") == true ||
                 path?.Contains("/identity/account/forgotpassword") == true ||
                 path?.Contains("/resetpassword") == true ||
                 path?.Contains("/forgotpassword") == true))
            {
                _logger.LogWarning("🚨 PHÁT HIỆN RESET PASSWORD từ user: {UserId}", userId);
                await logEventService.RecordAuthEventAsync("ResetPassword", userId, ipAddress, false);
            }

            // Bắt các hành động đổi email
            if (method == "POST" && 
                (path?.Contains("/identity/account/manage/changeemail") == true ||
                 path?.Contains("/identity/account/changeemail") == true ||
                 path?.Contains("/changeemail") == true ||
                 path?.Contains("/manage/changeemail") == true ||
                 path?.Contains("/identity/account/manage") == true))
            {
                _logger.LogWarning("🚨 PHÁT HIỆN ĐỔI EMAIL từ user: {UserId}", userId);
                await logEventService.RecordAuthEventAsync("ChangeEmail", userId, ipAddress, false);
            }

            // Bắt các hành động đăng nhập
            if (method == "POST" && 
                (path?.Contains("/identity/account/login") == true ||
                 path?.Contains("/identity/account/signin") == true ||
                 path?.Contains("/login") == true ||
                 path?.Contains("/signin") == true))
            {
                _logger.LogWarning("🚨 PHÁT HIỆN ĐĂNG NHẬP từ user: {UserId}", userId);
                await logEventService.RecordAuthEventAsync("Login", userId, ipAddress, true);
            }

            // Bắt các hành động đăng xuất
            if (method == "POST" && 
                (path?.Contains("/identity/account/logout") == true ||
                 path?.Contains("/identity/account/signout") == true ||
                 path?.Contains("/logout") == true ||
                 path?.Contains("/signout") == true))
            {
                _logger.LogWarning("🚨 PHÁT HIỆN ĐĂNG XUẤT từ user: {UserId}", userId);
                await logEventService.RecordAuthEventAsync("Logout", userId, ipAddress, true);
            }

            // Bắt các hành động đăng ký
            if (method == "POST" && 
                (path?.Contains("/identity/account/register") == true ||
                 path?.Contains("/identity/account/signup") == true ||
                 path?.Contains("/register") == true ||
                 path?.Contains("/signup") == true))
            {
                _logger.LogWarning("🚨 PHÁT HIỆN ĐĂNG KÝ từ user: {UserId}", userId);
                await logEventService.RecordAuthEventAsync("Register", userId, ipAddress, true);
            }

            // Bắt các hành động truy cập admin từ user account - CẢI THIỆN
            if ((method == "GET" || method == "POST") && 
                (path?.Contains("/admin") == true || 
                 path?.Contains("/useradmin") == true ||
                 path?.Contains("/alerts") == true ||
                 path?.Contains("/logs") == true ||
                 path?.Contains("/audit") == true ||
                 path?.Contains("/ipblocking") == true ||
                 path?.Contains("/accountmanagement") == true) &&
                userId != "anonymous" && 
                userId != "admin@gmail.com" && 
                userId != "kietpro@gmail.com" &&
                userId != "analyst@gmail.com")
            {
                _logger.LogWarning("🚨 PHÁT HIỆN USER TRUY CẬP ADMIN AREA từ user: {UserId} tại {Path}", userId, path);
                
                // Tạo alert ngay lập tức cho mỗi lần truy cập
                await logEventService.RecordSuspiciousEventAsync("AdminAccess", 
                    $"User {userId} attempted to access admin area: {path}", ipAddress, userId);
                
                // Ghi log auth event để tracking
                await logEventService.RecordAuthEventAsync("AdminAccess", userId, ipAddress, false);
            }
        }
    }

    // Extension method để dễ dàng đăng ký middleware
    public static class IdentityLoggingMiddlewareExtensions
    {
        public static IApplicationBuilder UseIdentityLogging(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<IdentityLoggingMiddleware>();
        }
    }
} 