using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using SecurityMonitor.Models;
using SecurityMonitor.DTOs;
using SecurityMonitor.Hubs;
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

        public UserAdminController(
            UserManager<ApplicationUser> userManager, 
            IHubContext<AccountHub> accountHub,
            ILogger<UserAdminController> logger)
        {
            _userManager = userManager;
            _accountHub = accountHub;
            _logger = logger;
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
                    return BadRequest("User not found");
                }

                if (await _userManager.IsInRoleAsync(user, "Admin"))
                {
                    _logger.LogWarning("Attempt to lock admin user: {UserName}", user.UserName);
                    return BadRequest("Cannot lock admin users");
                }

                user.LockoutEnd = DateTimeOffset.UtcNow.AddDays(days);
                var result = await _userManager.UpdateAsync(user);
                
                if (result.Succeeded)
                {
                    _logger.LogInformation("User {UserName} locked for {Days} days by admin {AdminName}", 
                        user.UserName, days, User.Identity?.Name);
                    
                    // Gửi thông báo real-time
                    await _accountHub.Clients.All.SendAsync("UserStatusUpdated", user.UserName, true, user.Id);
                    
                    return Ok(new { message = $"User {user.UserName} has been locked for {days} days" });
                }
                else
                {
                    _logger.LogError("Failed to lock user {UserName}: {Errors}", 
                        user.UserName, string.Join(", ", result.Errors.Select(e => e.Description)));
                    return BadRequest("Failed to lock user");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error locking user with ID: {UserId}", id);
                return StatusCode(500, "Internal server error");
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
                    return BadRequest("User not found");
                }

                user.LockoutEnd = null;
                var result = await _userManager.UpdateAsync(user);
                
                if (result.Succeeded)
                {
                    _logger.LogInformation("User {UserName} unlocked by admin {AdminName}", 
                        user.UserName, User.Identity?.Name);
                    
                    // Gửi thông báo real-time
                    await _accountHub.Clients.All.SendAsync("UserStatusUpdated", user.UserName, false, user.Id);
                    
                    return Ok(new { message = $"User {user.UserName} has been unlocked" });
                }
                else
                {
                    _logger.LogError("Failed to unlock user {UserName}: {Errors}", 
                        user.UserName, string.Join(", ", result.Errors.Select(e => e.Description)));
                    return BadRequest("Failed to unlock user");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unlocking user with ID: {UserId}", id);
                return StatusCode(500, "Internal server error");
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
                    return BadRequest("User not found");
                }

                if (await _userManager.IsInRoleAsync(user, "Admin"))
                {
                    _logger.LogWarning("Attempt to restrict admin user: {UserName}", user.UserName);
                    return BadRequest("Cannot restrict admin users");
                }

                user.IsRestricted = true;
                var result = await _userManager.UpdateAsync(user);
                
                if (result.Succeeded)
                {
                    _logger.LogInformation("User {UserName} restricted by admin {AdminName}", 
                        user.UserName, User.Identity?.Name);
                    
                    // Gửi thông báo real-time
                    await _accountHub.Clients.All.SendAsync("UserRestricted", user.UserName, "Vi phạm chính sách", user.Id);
                    
                    return Ok(new { message = $"User {user.UserName} has been restricted" });
                }
                else
                {
                    _logger.LogError("Failed to restrict user {UserName}: {Errors}", 
                        user.UserName, string.Join(", ", result.Errors.Select(e => e.Description)));
                    return BadRequest("Failed to restrict user");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error restricting user with ID: {UserId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost]
        public async Task<IActionResult> Unrestrict(string id)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(id);
                if (user == null)
                {
                    _logger.LogWarning("Attempt to unrestrict non-existent user with ID: {UserId}", id);
                    return BadRequest("User not found");
                }

                user.IsRestricted = false;
                var result = await _userManager.UpdateAsync(user);
                
                if (result.Succeeded)
                {
                    _logger.LogInformation("User {UserName} unrestricted by admin {AdminName}", 
                        user.UserName, User.Identity?.Name);
                    
                    // Gửi thông báo real-time
                    await _accountHub.Clients.All.SendAsync("UserUnrestricted", user.UserName, user.Id);
                    
                    return Ok(new { message = $"User {user.UserName} has been unrestricted" });
                }
                else
                {
                    _logger.LogError("Failed to unrestrict user {UserName}: {Errors}", 
                        user.UserName, string.Join(", ", result.Errors.Select(e => e.Description)));
                    return BadRequest("Failed to unrestrict user");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unrestricting user with ID: {UserId}", id);
                return StatusCode(500, "Internal server error");
            }
        }
    }
}
