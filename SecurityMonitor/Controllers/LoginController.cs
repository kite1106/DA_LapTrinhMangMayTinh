using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SecurityMonitor.Models;
using SecurityMonitor.Services;

namespace SecurityMonitor.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LoginController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly LoginMonitorService _loginMonitor;
    private readonly ILogger<LoginController> _logger;

    public LoginController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        LoginMonitorService loginMonitor,
        ILogger<LoginController> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _loginMonitor = loginMonitor;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> Login([FromBody] LoginRequest model)
    {
        // Lấy IP của client
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        // Kiểm tra user có tồn tại không
        var user = await _userManager.FindByNameAsync(model.Username);
        if (user == null)
        {
            // Ghi nhận thất bại và theo dõi IP
            await _loginMonitor.RecordLoginAttemptAsync(ipAddress, false, model.Username);
            return BadRequest("Tên đăng nhập hoặc mật khẩu không đúng");
        }

        // Kiểm tra mật khẩu
        var result = await _signInManager.CheckPasswordSignInAsync(user, model.Password, true);
        
        if (result.IsLockedOut)
        {
            _logger.LogWarning("🔒 Tài khoản {Username} bị khóa", model.Username);
            return BadRequest("Tài khoản đã bị khóa do đăng nhập sai nhiều lần");
        }

        if (!result.Succeeded)
        {
            // Ghi nhận thất bại và theo dõi IP
            await _loginMonitor.RecordLoginAttemptAsync(ipAddress, false, model.Username);
            return BadRequest("Tên đăng nhập hoặc mật khẩu không đúng");
        }

        // Đăng nhập thành công
        await _signInManager.SignInAsync(user, model.RememberMe);
        
        // Ghi nhận đăng nhập thành công
        await _loginMonitor.RecordLoginAttemptAsync(ipAddress, true, model.Username);

        // Cập nhật thông tin đăng nhập
        user.LastLoginAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        _logger.LogInformation("✅ User {Username} đăng nhập thành công từ IP {IP}", model.Username, ipAddress);

        return Ok(new
        {
            Username = user.UserName,
            Email = user.Email,
            FullName = user.FullName
        });
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return Ok();
    }
}

public class LoginRequest
{
    public string Username { get; set; } = null!;
    public string Password { get; set; } = null!;
    public bool RememberMe { get; set; }
}
