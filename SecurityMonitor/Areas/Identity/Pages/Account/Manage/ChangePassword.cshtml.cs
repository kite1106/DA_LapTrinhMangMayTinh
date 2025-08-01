// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using SecurityMonitor.Models;
using SecurityMonitor.Services.Interfaces;
using SecurityMonitor.Extensions;

namespace SecurityMonitor.Areas.Identity.Pages.Account.Manage
{
    public class ChangePasswordModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ILogger<ChangePasswordModel> _logger;
        private readonly ILogService _logService;
        private readonly IAlertService _alertService;

        public ChangePasswordModel(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            ILogger<ChangePasswordModel> logger,
            ILogService logService,
            IAlertService alertService)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _logger = logger;
            _logService = logService;
            _alertService = alertService;
        }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [BindProperty]
        public InputModel Input { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [TempData]
        public string StatusMessage { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public class InputModel
        {
            /// <summary>
            ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
            ///     directly from your code. This API may change or be removed in future releases.
            /// </summary>
            [Required]
            [DataType(DataType.Password)]
            [Display(Name = "Current password")]
            public string OldPassword { get; set; }

            /// <summary>
            ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
            ///     directly from your code. This API may change or be removed in future releases.
            /// </summary>
            [Required]
            [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
            [DataType(DataType.Password)]
            [Display(Name = "New password")]
            public string NewPassword { get; set; }

            /// <summary>
            ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
            ///     directly from your code. This API may change or be removed in future releases.
            /// </summary>
            [DataType(DataType.Password)]
            [Display(Name = "Confirm new password")]
            [Compare("NewPassword", ErrorMessage = "The new password and confirmation password do not match.")]
            public string ConfirmPassword { get; set; }
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            var hasPassword = await _userManager.HasPasswordAsync(user);
            if (!hasPassword)
            {
                return RedirectToPage("./SetPassword");
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            var changePasswordResult = await _userManager.ChangePasswordAsync(user, Input.OldPassword, Input.NewPassword);
            if (!changePasswordResult.Succeeded)
            {
                foreach (var error in changePasswordResult.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
                return Page();
            }

            await _signInManager.RefreshSignInAsync(user);
            _logger.LogInformation("User changed their password successfully.");
            
            // Tạo log entry cho hành động đổi password
            var clientIp = Request.GetClientIpAddress();
            var logEntry = new LogEntry
            {
                Message = $"User {user.UserName} changed password successfully",
                LogSourceId = 1, // Custom App
                LogLevelTypeId = 1, // Information
                IpAddress = clientIp,
                UserId = user.Id,
                WasSuccessful = true,
                Details = $"Password change completed for user {user.UserName} from IP {clientIp}"
            };
            
            await _logService.CreateLogAsync(logEntry);
            
            // Kiểm tra xem có đổi password quá nhiều lần không
            var recentPasswordChanges = await _logService.GetRecentLogsAsync(TimeSpan.FromHours(1));
            var userPasswordChanges = recentPasswordChanges
                .Where(l => l.UserId == user.Id && l.Message.Contains("changed password"))
                .Count();
            
            if (userPasswordChanges > 3) // Nếu đổi password hơn 3 lần trong 1 giờ
            {
                var alert = new Alert
                {
                    Title = "Suspicious Password Change Activity",
                    Description = $"User {user.UserName} has changed password {userPasswordChanges} times in the last hour",
                    AlertTypeId = (int)AlertTypeId.SuspiciousIP,
                    SeverityLevelId = (int)SeverityLevelId.High,
                    StatusId = (int)AlertStatusId.New,
                    SourceIp = clientIp,
                    LogId = logEntry.Id
                };
                
                await _alertService.CreateAlertAsync(alert);
            }
            
            StatusMessage = "Your password has been changed.";

            return RedirectToPage();
        }
    }
}
