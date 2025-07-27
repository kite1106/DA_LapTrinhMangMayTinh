using Microsoft.AspNetCore.Http;

namespace SecurityMonitor.Middleware
{
    public class RedirectHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<RedirectHandlingMiddleware> _logger;

        public RedirectHandlingMiddleware(RequestDelegate next, ILogger<RedirectHandlingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var originalPath = context.Request.Path;
            var originalMethod = context.Request.Method;

            _logger.LogInformation(
                "Request {method} {path} received with scheme {scheme}",
                originalMethod,
                originalPath,
                context.Request.Scheme);

            // Nếu request đến từ HTTP và cần HTTPS
            if (context.Request.Scheme == "http" && 
                !context.Request.Headers["X-Forwarded-Proto"].ToString().Equals("https", StringComparison.OrdinalIgnoreCase))
            {
                var httpsUrl = $"https://{context.Request.Host}{context.Request.Path}{context.Request.QueryString}";
                
                _logger.LogInformation(
                    "Redirecting {method} {path} to {newUrl}",
                    originalMethod,
                    originalPath,
                    httpsUrl);

                // Sử dụng 307 để giữ nguyên HTTP method
                context.Response.Redirect(httpsUrl, true);
                return;
            }

            // Xử lý các header từ proxy
            if (context.Request.Headers.ContainsKey("X-Forwarded-Proto"))
            {
                context.Request.Scheme = context.Request.Headers["X-Forwarded-Proto"].ToString();
            }

            await _next(context);
        }
    }
}
