using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SecurityMonitor.Models;
using SecurityMonitor.DTOs.Auth;
using System.Threading.Tasks;
using SecurityMonitor.Data;
using Microsoft.EntityFrameworkCore;

namespace SecurityMonitor.Controllers
{
    public class LoginController : Controller
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;

        public LoginController(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context,
            IConfiguration configuration)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _context = context;
            _configuration = configuration;
        }

        [HttpGet]
        public IActionResult Index()
        {
            if (User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Index", "Home");
            }
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Index(LoginDto model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
            {
                ModelState.AddModelError("", "Email hoặc mật khẩu không đúng");
                return View(model);
            }

            // Kiểm tra nếu tài khoản đang bị khóa
            if (await _userManager.IsLockedOutAsync(user))
            {
                ModelState.AddModelError("", $"Tài khoản đã bị khóa đến {user.LockoutEnd?.LocalDateTime:dd/MM/yyyy HH:mm}");
                return View(model);
            }

            var result = await _signInManager.PasswordSignInAsync(model.Email, model.Password, model.RememberMe, lockoutOnFailure: true);
            
            if (result.Succeeded)
            {
                // Cập nhật thông tin đăng nhập thành công
                user.LastLoginTime = DateTime.UtcNow;
                user.LastLoginIP = HttpContext.Connection.RemoteIpAddress?.ToString();
                await _userManager.UpdateAsync(user);

                // Ghi log
                await _context.AuditLogs.AddAsync(new AuditLog
                {
                    UserId = user.Id,
                    Action = "Login",
                    Details = $"Đăng nhập thành công từ IP {user.LastLoginIP}",
                    Timestamp = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();

                if (await _userManager.IsInRoleAsync(user, "Admin"))
                {
                    return RedirectToAction("Index", "Admin");
                }
                return RedirectToAction("Index", "Alerts");
            }

            if (result.IsLockedOut)
            {
                // Ghi log khi tài khoản bị khóa
                await _context.AuditLogs.AddAsync(new AuditLog
                {
                    UserId = user.Id,
                    Action = "AccountLocked",
                    Details = $"Tài khoản bị khóa do đăng nhập thất bại nhiều lần từ IP {HttpContext.Connection.RemoteIpAddress}",
                    Timestamp = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();

                ModelState.AddModelError("", "Tài khoản đã bị khóa do đăng nhập thất bại nhiều lần. Vui lòng thử lại sau.");
                return View(model);
            }

            ModelState.AddModelError("", "Email hoặc mật khẩu không đúng");
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            var userId = User.Identity.Name;
            await _signInManager.SignOutAsync();

            // Ghi log đăng xuất
            if (!string.IsNullOrEmpty(userId))
            {
                await _context.AuditLogs.AddAsync(new AuditLog
                {
                    UserId = userId,
                    Action = "Logout",
                    Details = "Đăng xuất thành công",
                    Timestamp = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();
            }

            return RedirectToAction("Index");
        }
    }
}
