using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecurityMonitor.Services.Interfaces;
using SecurityMonitor.Services.Implementation;
using SecurityMonitor.Models; // Added for Alert and AlertTypeId
using Microsoft.Extensions.DependencyInjection; // Added for CreateScope

namespace SecurityMonitor.Controllers
{
    [Authorize]
    public class TestController : Controller
    {
        private readonly ILogEventService _logEventService;
        private readonly ILogger<TestController> _logger;

        public TestController(ILogEventService logEventService, ILogger<TestController> logger)
        {
            _logEventService = logEventService;
            _logger = logger;
        }

        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        [Route("test/sensitive-endpoint")]
        public async Task<IActionResult> TestSensitiveEndpoint()
        {
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
            var userId = User.Identity?.Name ?? "testuser";

            // Test truy cập endpoint nhạy cảm
            await _logEventService.RecordApiEventAsync("/admin/users", "GET", userId, ipAddress, 403);
            
            return Json(new { success = true, message = "Đã test truy cập endpoint nhạy cảm" });
        }

        [HttpPost]
        [Route("test/scanner-detection")]
        public async Task<IActionResult> TestScanner()
        {
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
            var userId = User.Identity?.Name ?? "testuser";

            // Test scanner - nhiều request 404
            for (int i = 0; i < 15; i++)
            {
                await _logEventService.RecordApiEventAsync($"/admin/test{i}", "GET", userId, ipAddress, 404);
            }
            
            return Json(new { success = true, message = "Đã test scanner (15 request 404)" });
        }

        [HttpPost]
        [Route("test/password-reset")]
        public async Task<IActionResult> TestPasswordReset()
        {
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
            var userId = User.Identity?.Name ?? "testuser";

            // Test nhiều lần reset password
            for (int i = 0; i < 5; i++)
            {
                await _logEventService.RecordAuthEventAsync("ResetPassword", userId, ipAddress, false);
            }
            
            return Json(new { success = true, message = "Đã test nhiều lần reset password" });
        }

        [HttpPost]
        [Route("test/email-change")]
        public async Task<IActionResult> TestEmailChange()
        {
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
            var userId = User.Identity?.Name ?? "testuser";

            // Test nhiều lần thay đổi email
            for (int i = 0; i < 5; i++)
            {
                await _logEventService.RecordAuthEventAsync("ChangeEmail", userId, ipAddress, false);
            }
            
            return Json(new { success = true, message = "Đã test nhiều lần thay đổi email" });
        }

        [HttpPost]
        [Route("test/system-errors")]
        public async Task<IActionResult> TestSystemErrors()
        {
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

            // Test nhiều lỗi hệ thống
            for (int i = 0; i < 10; i++)
            {
                await _logEventService.RecordSystemEventAsync("Error", $"Test error {i}: Database connection failed", "TestService", ipAddress);
            }
            
            return Json(new { success = true, message = "Đã test nhiều lỗi hệ thống" });
        }

        [HttpPost]
        [Route("test/suspicious-keywords")]
        public async Task<IActionResult> TestSuspiciousKeywords()
        {
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

            // Test từ khóa đáng ngờ
            var suspiciousMessages = new[]
            {
                "SQL injection attempt detected",
                "XSS attack blocked",
                "CSRF token validation failed",
                "Script tag found in input",
                "Union select statement detected",
                "Drop table command blocked"
            };

            foreach (var message in suspiciousMessages)
            {
                await _logEventService.RecordSystemEventAsync("Security", message, "SecurityScanner", ipAddress);
            }
            
            return Json(new { success = true, message = "Đã test từ khóa đáng ngờ" });
        }

        [HttpPost]
        [Route("test/ddos-attack")]
        public async Task<IActionResult> TestDDoS()
        {
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
            var userId = User.Identity?.Name ?? "testuser";

            // Test DDoS - nhiều request liên tiếp
            for (int i = 0; i < 50; i++)
            {
                await _logEventService.RecordApiEventAsync("/api/test", "GET", userId, ipAddress, 200);
            }
            
            return Json(new { success = true, message = "Đã test DDoS (50 request liên tiếp)" });
        }

        [HttpPost]
        [Route("test/high-traffic")]
        public async Task<IActionResult> TestHighTraffic()
        {
            try
            {
                var userId = User.Identity?.Name ?? "testuser";
                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

                // Simulate high traffic
                for (int i = 0; i < 25; i++)
                {
                    await _logEventService.RecordApiEventAsync("/api/test", "GET", userId, ipAddress, 200);
                }

                return Json(new { success = true, message = "High traffic test completed" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        [HttpPost]
        [Route("test/admin-access")]
        public async Task<IActionResult> TestAdminAccess()
        {
            try
            {
                var userId = User.Identity?.Name ?? "testuser";
                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

                // Simulate admin access attempts
                await _logEventService.RecordAuthEventAsync("AdminAccess", userId, ipAddress, false);

                return Json(new { success = true, message = "Admin access test completed" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> TestAllScenarios()
        {
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
            var userId = User.Identity?.Name ?? "testuser";

            // Test tất cả các scenario
            await TestSensitiveEndpoint();
            await TestScanner();
            await TestPasswordReset();
            await TestEmailChange();
            await TestSystemErrors();
            await TestSuspiciousKeywords();
            await TestDDoS();
            await TestHighTraffic();
            
            return Json(new { success = true, message = "Đã test tất cả các scenario cảnh báo" });
        }

        [HttpPost]
        public async Task<IActionResult> TestRealPasswordChange()
        {
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
            var userId = User.Identity?.Name ?? "testuser";

            // Test đổi mật khẩu thật sự
            for (int i = 0; i < 3; i++)
            {
                // Gọi LogEventService để ghi log đổi mật khẩu
                await _logEventService.RecordAuthEventAsync("ChangePassword", userId, ipAddress, true);
                // Delay 1 giây giữa các lần đổi
                await Task.Delay(1000);
            }
            
            return Json(new { success = true, message = "Đã test đổi mật khẩu thật 3 lần" });
        }

        [HttpPost]
        public async Task<IActionResult> TestMiddlewareCapture()
        {
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
            var userId = User.Identity?.Name ?? "testuser";

            // Test xem middleware có bắt được request không
            await _logEventService.RecordAuthEventAsync("ChangePassword", userId, ipAddress, true);
            
            return Json(new { 
                success = true, 
                message = "Đã test middleware capture",
                userId = userId,
                ipAddress = ipAddress
            });
        }

        [HttpPost]
        public async Task<IActionResult> TestIdentityFormSubmission()
        {
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
            var userId = User.Identity?.Name ?? "testuser";

            // Simulate Identity form submission
            _logger.LogInformation("Testing Identity form submission for user: {UserId}", userId);
            
            // Test đổi mật khẩu 3 lần để trigger alert
            for (int i = 0; i < 3; i++)
            {
                await _logEventService.RecordAuthEventAsync("ChangePassword", userId, ipAddress, true);
                await Task.Delay(500); // Delay 0.5 giây
            }
            
            return Json(new { 
                success = true, 
                message = "Đã test Identity form submission 3 lần",
                userId = userId,
                ipAddress = ipAddress
            });
        }

        [HttpPost]
        public async Task<IActionResult> TestAlertCreation()
        {
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
            var userId = User.Identity?.Name ?? "testuser";

            _logger.LogInformation("Testing alert creation for user: {UserId}", userId);
            
            // Test tạo alert trực tiếp
            using var scope = HttpContext.RequestServices.CreateScope();
            var alertService = scope.ServiceProvider.GetRequiredService<IAlertService>();
            
            // Tạo alert test
            var alert = new Alert
            {
                Title = "Test Alert - Password Change",
                Description = $"User {userId} changed password multiple times",
                Timestamp = DateTime.UtcNow,
                SourceIp = ipAddress,
                AlertTypeId = (int)AlertTypeId.SuspiciousIP,
                SeverityLevelId = (int)SeverityLevelId.High,
                StatusId = 1 // Active status
            };
            
            await alertService.CreateAlertAsync(alert);
            
            return Json(new { 
                success = true, 
                message = "Đã tạo test alert",
                userId = userId,
                ipAddress = ipAddress
            });
        }

        [HttpPost]
        public async Task<IActionResult> TestFailedLogin()
        {
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
            var userId = User.Identity?.Name ?? "testuser";

            _logger.LogInformation("Testing failed login for user: {UserId}", userId);
            
            // Test failed login
            await _logEventService.RecordAuthEventAsync("Login", userId, ipAddress, false);
            
            return Json(new { 
                success = true, 
                message = "Đã test failed login",
                userId = userId,
                ipAddress = ipAddress
            });
        }

        [HttpGet]
        public IActionResult ChangePassword()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ChangePassword(string currentPassword, string newPassword, string confirmPassword)
        {
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
            var userId = User.Identity?.Name ?? "testuser";

            if (newPassword != confirmPassword)
            {
                return Json(new { success = false, message = "Mật khẩu mới không khớp" });
            }

            // Ghi log đổi mật khẩu
            await _logEventService.RecordAuthEventAsync("ChangePassword", userId, ipAddress, true);
            
            return Json(new { success = true, message = "Đã ghi log đổi mật khẩu" });
        }
    }
} 