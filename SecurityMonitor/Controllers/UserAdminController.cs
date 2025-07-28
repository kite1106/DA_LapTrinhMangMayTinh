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

namespace SecurityMonitor.Controllers
{
    [Authorize(Roles = "Admin")]
    public class UserAdminController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IHubContext<AccountHub> _accountHub;

        public UserAdminController(UserManager<ApplicationUser> userManager, IHubContext<AccountHub> accountHub)
        {
            _userManager = userManager;
            _accountHub = accountHub;
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
            var user = await _userManager.FindByIdAsync(id);
            if (user == null || await _userManager.IsInRoleAsync(user, "Admin"))
                return BadRequest();

            user.LockoutEnd = DateTimeOffset.UtcNow.AddDays(days);
            await _userManager.UpdateAsync(user);
            await _accountHub.Clients.All.SendAsync("UserStatusUpdated", user.UserName, true, user.Id);
            return Ok();
        }

        [HttpPost]
        public async Task<IActionResult> Unlock(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return BadRequest();

            user.LockoutEnd = null;
            await _userManager.UpdateAsync(user);
            await _accountHub.Clients.All.SendAsync("UserStatusUpdated", user.UserName, false, user.Id);
            return Ok();
        }

        [HttpPost]
        public async Task<IActionResult> Restrict(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return BadRequest();

            user.IsRestricted = true;
            await _userManager.UpdateAsync(user);
            await _accountHub.Clients.All.SendAsync("UserRestricted", user.UserName, "Vi phạm chính sách", user.Id);
            return Ok();
        }

        [HttpPost]
        public async Task<IActionResult> Unrestrict(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return BadRequest();

            user.IsRestricted = false;
            await _userManager.UpdateAsync(user);
            await _accountHub.Clients.All.SendAsync("UserStatusUpdated", user.UserName, false, user.Id);
            return Ok();
        }
    }
}
