using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using SecurityMonitor.Models;
using SecurityMonitor.DTOs;
using SecurityMonitor.Hubs;
using SecurityMonitor.Services.Interfaces;
using SecurityMonitor.Extensions;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace SecurityMonitor.Controllers
{
    [Authorize(Roles = "Admin")]
    public class UserAdminController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IHubContext<AccountHub> _accountHub;
        private readonly ILogger<UserAdminController> _logger;
        private readonly ILogService _logService;
        private readonly IAlertService _alertService;

        public UserAdminController(
            UserManager<ApplicationUser> userManager, 
            IHubContext<AccountHub> accountHub,
            ILogger<UserAdminController> logger,
            ILogService logService,
            IAlertService alertService)
        {
            _userManager = userManager;
            _accountHub = accountHub;
            _logger = logger;
            _logService = logService;
            _alertService = alertService;
        }

        public async Task<IActionResult> Index()
        {
            var users = await _userManager.Users.ToListAsync();
            var userDtos = new List<UserManagementDto>();
            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                userDtos.Add(new UserManagementDto
                {
                    Id = user.Id,
                    Email = user.Email,
                    UserName = user.UserName,
                    IsLocked = user.LockoutEnd != null && user.LockoutEnd > DateTimeOffset.UtcNow,
                    LockoutEnd = user.LockoutEnd,
                    LastLoginTime = user.LastLoginTime,
                    FailedAccessCount = user.AccessFailedCount,
                    Roles = roles.ToList(),
                    IsRestricted = user.IsRestricted
                });
            }
            
            // Log access to sensitive admin page
            var currentUser = await _userManager.GetUserAsync(User);
            var clientIp = Request.GetClientIpAddress();
            var logEntry = new LogEntry
            {
                Message = $"Admin {currentUser?.UserName} accessed User Management page",
                LogSourceId = 1, // Custom App
                LogLevelTypeId = 1, // Information
                IpAddress = clientIp,
                UserId = currentUser?.Id,
                WasSuccessful = true,
                Details = $"Admin access to user management from IP {clientIp}"
            };
            
            await _logService.CreateLogAsync(logEntry);
            
            return View(userDtos);
        }

        [HttpPost]
        public async Task<IActionResult> Lock(string id, int days = 7)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(id);
                if (user == null)
                {
                    _logger.LogWarning("Attempt to lock non-existent user with ID: {UserId}", id);
                    return Json(new { success = false, message = "User not found" });
                }

                if (await _userManager.IsInRoleAsync(user, "Admin"))
                {
                    _logger.LogWarning("Attempt to lock admin user: {UserName}", user.UserName);
                    return Json(new { success = false, message = "Cannot lock admin users" });
                }

                user.LockoutEnd = DateTimeOffset.UtcNow.AddDays(days);
                var result = await _userManager.UpdateAsync(user);
                
                if (result.Succeeded)
                {
                    _logger.LogInformation("User {UserName} locked for {Days} days by admin {AdminName}", 
                        user.UserName, days, User.Identity?.Name);
                    
                    // Log the admin action
                    var currentUser = await _userManager.GetUserAsync(User);
                    var clientIp = Request.GetClientIpAddress();
                    var logEntry = new LogEntry
                    {
                        Message = $"Admin {currentUser?.UserName} locked user {user.UserName} for {days} days",
                        LogSourceId = 1, // Custom App
                        LogLevelTypeId = 2, // Warning
                        IpAddress = clientIp,
                        UserId = currentUser?.Id,
                        WasSuccessful = true,
                        Details = $"User lock action: {user.UserName} locked by {currentUser?.UserName} from IP {clientIp}"
                    };
                    
                    await _logService.CreateLogAsync(logEntry);
                    
                    // Gửi thông báo real-time - User bị block
                    await _accountHub.Clients.All.SendAsync("UserBlocked", user.UserName, $"Tài khoản bị khóa {days} ngày", user.Id);
                    await _accountHub.Clients.User(user.Id).SendAsync("ForceLogout", "Tài khoản của bạn đã bị khóa", $"Tài khoản bị khóa {days} ngày");
                    
                    return Json(new { success = true, message = $"User {user.UserName} has been locked for {days} days" });
                }
                else
                {
                    _logger.LogError("Failed to lock user {UserName}: {Errors}", 
                        user.UserName, string.Join(", ", result.Errors.Select(e => e.Description)));
                    return Json(new { success = false, message = "Failed to lock user" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error locking user with ID: {UserId}", id);
                return Json(new { success = false, message = "Internal server error" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> Unlock(string id)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(id);
                if (user == null)
                {
                    _logger.LogWarning("Attempt to unlock non-existent user with ID: {UserId}", id);
                    return Json(new { success = false, message = "User not found" });
                }

                user.LockoutEnd = null;
                var result = await _userManager.UpdateAsync(user);
                
                if (result.Succeeded)
                {
                    _logger.LogInformation("User {UserName} unlocked by admin {AdminName}", 
                        user.UserName, User.Identity?.Name);
                    
                    // Gửi thông báo real-time
                    await _accountHub.Clients.All.SendAsync("UserStatusUpdated", user.UserName, false, user.Id);
                    
                    return Json(new { success = true, message = $"User {user.UserName} has been unlocked" });
                }
                else
                {
                    _logger.LogError("Failed to unlock user {UserName}: {Errors}", 
                        user.UserName, string.Join(", ", result.Errors.Select(e => e.Description)));
                    return Json(new { success = false, message = "Failed to unlock user" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unlocking user with ID: {UserId}", id);
                return Json(new { success = false, message = "Internal server error" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> Restrict(string id)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(id);
                if (user == null)
                {
                    _logger.LogWarning("Attempt to restrict non-existent user with ID: {UserId}", id);
                    return Json(new { success = false, message = "User not found" });
                }

                if (await _userManager.IsInRoleAsync(user, "Admin"))
                {
                    _logger.LogWarning("Attempt to restrict admin user: {UserName}", user.UserName);
                    return Json(new { success = false, message = "Cannot restrict admin users" });
                }

                user.IsRestricted = true;
                var result = await _userManager.UpdateAsync(user);
                
                if (result.Succeeded)
                {
                    _logger.LogInformation("User {UserName} restricted by admin {AdminName}", 
                        user.UserName, User.Identity?.Name);
                    
                    // Log the admin action
                    var currentUser = await _userManager.GetUserAsync(User);
                    var clientIp = Request.GetClientIpAddress();
                    var logEntry = new LogEntry
                    {
                        Message = $"Admin {currentUser?.UserName} restricted user {user.UserName}",
                        LogSourceId = 1, // Custom App
                        LogLevelTypeId = 2, // Warning
                        IpAddress = clientIp,
                        UserId = currentUser?.Id,
                        WasSuccessful = true,
                        Details = $"User restriction action: {user.UserName} restricted by {currentUser?.UserName} from IP {clientIp}"
                    };
                    
                    await _logService.CreateLogAsync(logEntry);
                    
                    // Gửi thông báo real-time
                    await _accountHub.Clients.All.SendAsync("UserRestricted", user.UserName, "Vi phạm chính sách", user.Id);
                    await _accountHub.Clients.User(user.Id).SendAsync("UserRestricted", user.UserName, "Vi phạm chính sách", user.Id);
                    
                    return Json(new { success = true, message = $"User {user.UserName} has been restricted" });
                }
                else
                {
                    _logger.LogError("Failed to restrict user {UserName}: {Errors}", 
                        user.UserName, string.Join(", ", result.Errors.Select(e => e.Description)));
                    return Json(new { success = false, message = "Failed to restrict user" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error restricting user with ID: {UserId}", id);
                return Json(new { success = false, message = "Internal server error" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> RemoveRestriction(string id)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(id);
                if (user == null)
                {
                    _logger.LogWarning("Attempt to remove restriction from non-existent user with ID: {UserId}", id);
                    return Json(new { success = false, message = "User not found" });
                }

                user.IsRestricted = false;
                var result = await _userManager.UpdateAsync(user);
                
                if (result.Succeeded)
                {
                    _logger.LogInformation("User {UserName} restriction removed by admin {AdminName}", 
                        user.UserName, User.Identity?.Name);
                    
                    // Gửi thông báo real-time
                    await _accountHub.Clients.All.SendAsync("UserUnrestricted", user.UserName, user.Id);
                    
                    return Json(new { success = true, message = $"User {user.UserName} restriction has been removed" });
                }
                else
                {
                    _logger.LogError("Failed to remove restriction from user {UserName}: {Errors}", 
                        user.UserName, string.Join(", ", result.Errors.Select(e => e.Description)));
                    return Json(new { success = false, message = "Failed to remove restriction" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing restriction from user with ID: {UserId}", id);
                return Json(new { success = false, message = "Internal server error" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> Delete(string id)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(id);
                if (user == null)
                {
                    _logger.LogWarning("Attempt to delete non-existent user with ID: {UserId}", id);
                    return Json(new { success = false, message = "User not found" });
                }

                if (await _userManager.IsInRoleAsync(user, "Admin"))
                {
                    _logger.LogWarning("Attempt to delete admin user: {UserName}", user.UserName);
                    return Json(new { success = false, message = "Cannot delete admin users" });
                }

                var result = await _userManager.DeleteAsync(user);
                
                if (result.Succeeded)
                {
                    _logger.LogInformation("User {UserName} deleted by admin {AdminName}", 
                        user.UserName, User.Identity?.Name);
                    
                    return Json(new { success = true, message = $"User {user.UserName} has been deleted" });
                }
                else
                {
                    _logger.LogError("Failed to delete user {UserName}: {Errors}", 
                        user.UserName, string.Join(", ", result.Errors.Select(e => e.Description)));
                    return Json(new { success = false, message = "Failed to delete user" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting user with ID: {UserId}", id);
                return Json(new { success = false, message = "Internal server error" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateUserDto createUserDto)
        {
            try
            {
                if (string.IsNullOrEmpty(createUserDto.Email) || string.IsNullOrEmpty(createUserDto.UserName) || string.IsNullOrEmpty(createUserDto.Password))
                {
                    return Json(new { success = false, message = "Email, username and password are required" });
                }

                var user = new ApplicationUser
                {
                    UserName = createUserDto.UserName,
                    Email = createUserDto.Email,
                    EmailConfirmed = true,
                    IsActive = true
                };

                var result = await _userManager.CreateAsync(user, createUserDto.Password);
                
                if (result.Succeeded)
                {
                    // Thêm role nếu có
                    if (!string.IsNullOrEmpty(createUserDto.Role))
                    {
                        await _userManager.AddToRoleAsync(user, createUserDto.Role);
                    }
                    else
                    {
                        await _userManager.AddToRoleAsync(user, "User");
                    }

                    _logger.LogInformation("User {UserName} created by admin {AdminName}", 
                        user.UserName, User.Identity?.Name);
                    
                    return Json(new { success = true, message = $"User {user.UserName} has been created successfully" });
                }
                else
                {
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    _logger.LogError("Failed to create user {UserName}: {Errors}", 
                        createUserDto.UserName, errors);
                    return Json(new { success = false, message = $"Failed to create user: {errors}" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating user");
                return Json(new { success = false, message = "Internal server error" });
            }
        }
    }

    public class CreateUserDto
    {
        public string Email { get; set; } = "";
        public string UserName { get; set; } = "";
        public string Password { get; set; } = "";
        public string Role { get; set; } = "";
    }
}
