using Microsoft.AspNetCore.Identity;
using SecurityMonitor.Models;
using System.Security.Claims;

namespace SecurityMonitor.Middleware
{
    public class RestrictedUserMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<RestrictedUserMiddleware> _logger;

        public RestrictedUserMiddleware(RequestDelegate next, ILogger<RestrictedUserMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, UserManager<ApplicationUser> userManager)
        {
            // Chỉ kiểm tra nếu user đã đăng nhập
            if (context.User.Identity?.IsAuthenticated == true)
            {
                var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!string.IsNullOrEmpty(userId))
                {
                    var user = await userManager.FindByIdAsync(userId);
                    if (user != null && user.IsRestricted)
                    {
                        var path = context.Request.Path.Value?.ToLower();
                        
                        // Cho phép truy cập một số trang cần thiết
                        var allowedPaths = new[]
                        {
                            "/user/index",
                            "/user/logout",
                            "/login/logout",
                            "/identity/account/logout",
                            "/css/",
                            "/js/",
                            "/lib/",
                            "/images/",
                            "/sounds/",
                            "/favicon.ico"
                        };

                        // Kiểm tra xem path hiện tại có được phép không
                        var isAllowed = allowedPaths.Any(allowedPath => 
                            path?.StartsWith(allowedPath) == true);

                        if (!isAllowed)
                        {
                            _logger.LogWarning("Restricted user {UserName} attempted to access {Path}", 
                                user.UserName, path);
                            
                            // Chuyển hướng về User Dashboard
                            context.Response.Redirect("/User/Index");
                            return;
                        }
                    }
                }
            }

            await _next(context);
        }
    }

    // Extension method để dễ dàng đăng ký middleware
    public static class RestrictedUserMiddlewareExtensions
    {
        public static IApplicationBuilder UseRestrictedUserCheck(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<RestrictedUserMiddleware>();
        }
    }
} 