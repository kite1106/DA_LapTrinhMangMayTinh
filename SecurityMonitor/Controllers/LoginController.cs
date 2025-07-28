using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SecurityMonitor.Models;
using SecurityMonitor.DTOs.Auth;
using System.Threading.Tasks;
using SecurityMonitor.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using SecurityMonitor.Hubs;
using System;
using Microsoft.Extensions.Logging;
using System.Linq;

namespace SecurityMonitor.Controllers
{
    public class LoginController : Controller
    {
        private const int MAX_LOGIN_ATTEMPTS = 5;
        private const int LOCKOUT_DURATION_MINUTES = 30;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly IHubContext<AlertHub> _alertHub;
        private readonly ILogger<LoginController> _logger;

        public LoginController(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context,
            IConfiguration configuration,
            IHubContext<AlertHub> alertHub,
            ILogger<LoginController> logger)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _context = context;
            _configuration = configuration;
            _alertHub = alertHub;
            _logger = logger;
        }

        private async Task SendLoginAlert(string message, string alertType, string? ipAddress)
        {
            try
            {
                await _alertHub.Clients.All.SendAsync("ReceiveLoginAlert", new
                {
                    message = message,
                    type = alertType,
                    ip = ipAddress ?? "unknown",
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending login alert");
            }
        }

        [HttpGet]
        public IActionResult Index()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Index", "Home");
            }
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Index(LoginDto model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

            // Check if IP is blocked
            var isBlocked = await _context.BlockedIPs
                .AnyAsync(b => b.IpAddress == ipAddress);
            if (isBlocked)
            {
                ModelState.AddModelError("", "IP của bạn đã bị chặn do nhiều lần đăng nhập thất bại");
                _logger.LogWarning("Blocked IP {IP} attempted to login", ipAddress);
                await SendLoginAlert($"IP {ipAddress} bị chặn cố gắng đăng nhập", "danger", ipAddress);
                return View(model);
            }

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
            {
                ModelState.AddModelError("", "Email hoặc mật khẩu không đúng");
                _logger.LogWarning("Login attempt with non-existent email from IP: {IP}", ipAddress);
                await SendLoginAlert($"Đăng nhập thất bại với email không tồn tại từ IP: {ipAddress}", "warning", ipAddress);
                return View(model);
            }

            if (user.IsRestricted)
            {
                ModelState.AddModelError("", "Tài khoản của bạn đã bị hạn chế. Vui lòng liên hệ quản trị viên.");
                _logger.LogWarning("Restricted user {Email} attempted to login from IP: {IP}", 
                    model.Email, ipAddress);
                await SendLoginAlert($"Tài khoản bị hạn chế {model.Email} cố gắng đăng nhập", "warning", ipAddress);
                return View(model);
            }

            if (!user.IsActive)
            {
                ModelState.AddModelError("", "Tài khoản chưa được kích hoạt");
                _logger.LogWarning("Inactive user {Email} attempted to login from IP: {IP}", 
                    model.Email, ipAddress);
                await SendLoginAlert($"Tài khoản chưa kích hoạt {model.Email} cố gắng đăng nhập", "warning", ipAddress);
                return View(model);
            }

            // Check for too many failed attempts from this IP
            var recentFailures = await _context.AuditLogs
                .Where(l => l.IpAddress == ipAddress && 
                           l.Action == "LoginFailed" && 
                           l.Timestamp >= DateTime.UtcNow.AddMinutes(-LOCKOUT_DURATION_MINUTES))
                .CountAsync();

            if (recentFailures >= MAX_LOGIN_ATTEMPTS)
            {
                ModelState.AddModelError("", $"IP của bạn đã bị tạm khóa do đăng nhập thất bại {MAX_LOGIN_ATTEMPTS} lần liên tiếp");
                
                // Create blocked IP record
                var blockedIP = new BlockedIP(
                    ipAddress ?? "unknown",
                    $"Quá nhiều lần đăng nhập thất bại ({recentFailures} lần)",
                    "System");
                _context.BlockedIPs.Add(blockedIP);

                // Create high severity alert
                var alertType = await _context.AlertTypes.FirstOrDefaultAsync(at => at.Name == "BruteForceAttempt");
                var severityLevel = await _context.SeverityLevels.FirstOrDefaultAsync(sl => sl.Name == "High");
                var alertStatus = await _context.AlertStatuses.FirstOrDefaultAsync(s => s.Name == "Active");

                if (alertType != null && severityLevel != null && alertStatus != null)
                {
                    var alert = new Alert
                    {
                        Title = "Phát hiện tấn công Brute Force",
                        Description = $"IP {ipAddress} đã bị chặn do {recentFailures} lần đăng nhập thất bại trong {LOCKOUT_DURATION_MINUTES} phút",
                        AlertTypeId = alertType.Id,
                        SeverityLevelId = severityLevel.Id,
                        StatusId = alertStatus.Id,
                        SourceIp = ipAddress,
                        Timestamp = DateTime.UtcNow
                    };
                    _context.Alerts.Add(alert);
                }

                await _context.SaveChangesAsync();
                await SendLoginAlert($"IP {ipAddress} đã bị chặn do nhiều lần đăng nhập thất bại", "danger", ipAddress);

                return View(model);
            }

            // Try to sign in
            var result = await _signInManager.PasswordSignInAsync(model.Email, model.Password, model.RememberMe, lockoutOnFailure: true);
            
            if (result.Succeeded)
            {
                // Update login info
                user.LastLoginTime = DateTime.UtcNow;
                user.LastLoginIP = ipAddress;
                await _userManager.UpdateAsync(user);

                // Log successful login
                var auditLog = new AuditLog
                {
                    UserId = user.Id,
                    IpAddress = ipAddress,
                    Action = "Login",
                    Details = $"Đăng nhập thành công từ IP {ipAddress}",
                    Timestamp = DateTime.UtcNow,
                    Level = "Information",
                    StatusCode = 200
                };
                _context.AuditLogs.Add(auditLog);
                await _context.SaveChangesAsync();

                await SendLoginAlert($"Đăng nhập thành công: {model.Email}", "success", ipAddress);

                if (await _userManager.IsInRoleAsync(user, "Admin"))
                {
                    return RedirectToAction("Index", "Admin");
                }
                return RedirectToAction("Index", "Alerts");
            }

            if (result.IsLockedOut)
            {
                ModelState.AddModelError("", $"Tài khoản đã bị khóa đến {user.LockoutEnd?.LocalDateTime:dd/MM/yyyy HH:mm}");
                _logger.LogWarning("User {Email} account locked out", model.Email);
                await SendLoginAlert($"Tài khoản {model.Email} đã bị khóa", "danger", ipAddress);

                var auditLog = new AuditLog
                {
                    UserId = user.Id,
                    IpAddress = ipAddress,
                    Action = "LoginFailed",
                    Details = $"Tài khoản bị khóa khi cố gắng đăng nhập từ IP {ipAddress}",
                    Timestamp = DateTime.UtcNow,
                    Level = "Warning",
                    StatusCode = 403
                };
                _context.AuditLogs.Add(auditLog);
                await _context.SaveChangesAsync();

                return View(model);
            }

            // If we got this far, login failed
            var failureAuditLog = new AuditLog
            {
                UserId = user.Id,
                IpAddress = ipAddress,
                Action = "LoginFailed",
                Details = $"Đăng nhập thất bại từ IP {ipAddress}",
                Timestamp = DateTime.UtcNow,
                Level = "Warning",
                StatusCode = 401
            };
            _context.AuditLogs.Add(failureAuditLog);
            await _context.SaveChangesAsync();

            // Check if we should create an alert for multiple failures
            if (recentFailures + 1 >= 3) // Alert after 3 failures
            {
                var alertType = await _context.AlertTypes.FirstOrDefaultAsync(at => at.Name == "LoginFailure");
                var severityLevel = await _context.SeverityLevels.FirstOrDefaultAsync(sl => sl.Name == "Medium");
                var alertStatus = await _context.AlertStatuses.FirstOrDefaultAsync(s => s.Name == "Active");

                if (alertType != null && severityLevel != null && alertStatus != null)
                {
                    var alert = new Alert
                    {
                        Title = "Phát hiện nhiều lần đăng nhập thất bại",
                        Description = $"Tài khoản {user.Email} đăng nhập sai {recentFailures + 1} lần từ IP {ipAddress}",
                        AlertTypeId = alertType.Id,
                        SeverityLevelId = severityLevel.Id,
                        StatusId = alertStatus.Id,
                        SourceIp = ipAddress,
                        Timestamp = DateTime.UtcNow
                    };
                    _context.Alerts.Add(alert);
                    await _context.SaveChangesAsync();
                }
            }

            _logger.LogWarning("Invalid login attempt for user {Email} from IP: {IP}", 
                model.Email, ipAddress);
            await SendLoginAlert($"Đăng nhập thất bại cho tài khoản {model.Email}", "warning", ipAddress);

            ModelState.AddModelError("", "Email hoặc mật khẩu không đúng");
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            var userId = User.Identity?.Name;
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            await _signInManager.SignOutAsync();

            // Log logout
            if (!string.IsNullOrEmpty(userId))
            {
                await _context.AuditLogs.AddAsync(new AuditLog
                {
                    UserId = userId,
                    Action = "Logout",
                    Details = "Đăng xuất thành công",
                    Timestamp = DateTime.UtcNow,
                    IpAddress = ipAddress ?? "unknown",
                    Level = "Information",
                    StatusCode = 200
                });
                await _context.SaveChangesAsync();

                await SendLoginAlert($"Người dùng {userId} đã đăng xuất", "info", ipAddress);
            }

            return RedirectToAction("Index");
        }
    }
}
