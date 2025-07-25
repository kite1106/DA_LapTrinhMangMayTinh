using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using SecurityMonitor.Models;
using SecurityMonitor.Services;

namespace SecurityMonitor.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class LoginModel : PageModel
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<LoginModel> _logger;
        private readonly LoginMonitorService _loginMonitor;

        public LoginModel(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            ILogger<LoginModel> logger,
            LoginMonitorService loginMonitor)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _logger = logger;
            _loginMonitor = loginMonitor;
        }

        [BindProperty]
        public required InputModel Input { get; set; }

        public required IList<AuthenticationScheme> ExternalLogins { get; set; } = new List<AuthenticationScheme>();

        public required string ReturnUrl { get; set; } = "/";

        [TempData]
        public string? ErrorMessage { get; set; }

        public class InputModel
        {
            [Required]
            [EmailAddress]
            public required string Email { get; set; }

            [Required]
            [DataType(DataType.Password)]
            public required string Password { get; set; }

            [Display(Name = "Ghi nhớ đăng nhập")]
            public bool RememberMe { get; set; }
        }

        public async Task OnGetAsync(string? returnUrl = null)
        {
            if (!string.IsNullOrEmpty(ErrorMessage))
            {
                ModelState.AddModelError(string.Empty, ErrorMessage);
            }

            returnUrl ??= Url.Content("~/");

            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

            ReturnUrl = returnUrl;
        }

        public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
        {
            returnUrl = "/Account/RedirectAfterLogin";

            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

            if (ModelState.IsValid)
            {
                var result = await _signInManager.PasswordSignInAsync(Input.Email, Input.Password, Input.RememberMe, lockoutOnFailure: true);

                // Ghi nhận kết quả đăng nhập
                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                await _loginMonitor.RecordLoginAttemptAsync(ipAddress, result.Succeeded, Input.Email);

                if (result.Succeeded)
                {
                    var user = await _userManager.FindByEmailAsync(Input.Email);
                    if (user == null)
                    {
                        ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                        return Page();
                    }

                    var roles = await _userManager.GetRolesAsync(user);

                    // ✅ Gán role "User" nếu chưa có
                    if (roles.Count == 0)
                    {
                        await _userManager.AddToRoleAsync(user, "User");
                        roles = await _userManager.GetRolesAsync(user);
                    }

                    // ✅ Cập nhật thời gian đăng nhập
                    user.LastLoginAt = DateTime.Now;
                    await _userManager.UpdateAsync(user);

                    _logger.LogInformation("User logged in.");

                    // ✅ Điều hướng theo role
                    if (roles.Contains("Admin"))
                        return LocalRedirect("/Admin/Index");
                    if (roles.Contains("Analyst"))
                        return LocalRedirect("/Analyst/Index");

                    return LocalRedirect("/User/Index");
                }

                if (result.RequiresTwoFactor)
                {
                    return RedirectToPage("./LoginWith2fa", new { ReturnUrl = returnUrl, RememberMe = Input.RememberMe });
                }

                if (result.IsLockedOut)
                {
                    _logger.LogWarning("User account locked out.");
                    return RedirectToPage("./Lockout");
                }

                ModelState.AddModelError(string.Empty, "Đăng nhập không hợp lệ.");
            }

            return Page();
        }
    }
}
