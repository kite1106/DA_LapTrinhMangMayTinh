using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecurityMonitor.Data;
using SecurityMonitor.Models;
using SecurityMonitor.DTOs;
using System.Threading.Tasks;

namespace SecurityMonitor.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AccountManagementController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ApplicationDbContext _context;

        public AccountManagementController(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            ApplicationDbContext context)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var users = await _userManager.Users
                .Select(u => new UserManagementDto
                {
                    Id = u.Id,
                    Email = u.Email,
                    UserName = u.UserName,
                    IsLocked = u.LockoutEnd != null && u.LockoutEnd > DateTimeOffset.UtcNow,
                    LockoutEnd = u.LockoutEnd,
                    LastLoginTime = u.LastLoginTime,
                    FailedAccessCount = u.AccessFailedCount,
                    Roles = _userManager.GetRolesAsync(u).Result
                })
                .ToListAsync();

            return View(users);
        }

        [HttpPost]
        public async Task<IActionResult> LockUser(string userName, int days, string reason, bool disableLogin = false)
        {
            var user = await _userManager.FindByNameAsync(userName);
            if (user == null)
            {
                return Json(new { success = false, message = "Không tìm thấy người dùng" });
            }

            // Không cho phép khóa tài khoản Admin
            if (await _userManager.IsInRoleAsync(user, "Admin"))
            {
                return Json(new { success = false, message = "Không thể khóa tài khoản Admin" });
            }

            var endDate = DateTimeOffset.UtcNow.AddDays(days);
            
            // Cập nhật thông tin khóa
            user.LockoutEnd = endDate;
            user.LockoutStart = DateTimeOffset.UtcNow;
            user.LockoutReason = reason;
            user.IsActive = !disableLogin; // Vô hiệu hóa đăng nhập nếu được yêu cầu
            
            var result = await _userManager.UpdateAsync(user);
            
            if (result.Succeeded)
            {
                // Ghi log
                await _context.AuditLogs.AddAsync(new AuditLog
                {
                    UserId = User.Identity?.Name,
                    Action = "LockUser",
                    Details = $"Khóa tài khoản {user.Email} trong {days} ngày. Lý do: {reason}." + 
                             (disableLogin ? " Đã vô hiệu hóa đăng nhập." : ""),
                    Timestamp = DateTime.UtcNow
                });

                // Thêm thông tin hạn chế tài khoản
                await _context.AccountRestrictions.AddAsync(new AccountRestriction
                {
                    UserId = user.Id,
                    RestrictedBy = User.Identity?.Name,
                    RestrictionType = disableLogin ? "Disable" : "Temporary",
                    Reason = reason,
                    StartTime = DateTimeOffset.UtcNow,
                    EndTime = endDate,
                    IsActive = true
                });

                await _context.SaveChangesAsync();
                
                return Json(new { 
                    success = true, 
                    message = $"Đã khóa tài khoản {user.Email} trong {days} ngày" +
                             (disableLogin ? " và vô hiệu hóa đăng nhập" : "")
                });
            }

            return Json(new { success = false, message = "Không thể khóa tài khoản" });
        }

        [HttpPost]
        public async Task<IActionResult> UnlockUser(string userName)
        {
            var user = await _userManager.FindByNameAsync(userName);
            if (user == null)
            {
                return Json(new { success = false, message = "Không tìm thấy người dùng" });
            }

            var result = await _userManager.SetLockoutEndDateAsync(user, null);
            if (result.Succeeded)
            {
                // Reset failed access count
                await _userManager.ResetAccessFailedCountAsync(user);
                
                await _context.AuditLogs.AddAsync(new AuditLog
                {
                    UserId = User.Identity.Name,
                    Action = "UnlockUser",
                    Details = $"Mở khóa tài khoản {user.Email}",
                    Timestamp = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();
                
                return Json(new { success = true, message = $"Đã mở khóa tài khoản {user.Email}" });
            }

            return Json(new { success = false, message = "Không thể mở khóa tài khoản" });
        }

        [HttpPost]
        public async Task<IActionResult> UpdateRole(string userName, string role)
        {
            var user = await _userManager.FindByNameAsync(userName);
            if (user == null)
            {
                return Json(new { success = false, message = "Không tìm thấy người dùng" });
            }

            // Không cho phép thay đổi role của Admin
            if (await _userManager.IsInRoleAsync(user, "Admin") && role != "Admin")
            {
                return Json(new { success = false, message = "Không thể thay đổi quyền của tài khoản Admin" });
            }

            var existingRoles = await _userManager.GetRolesAsync(user);
            await _userManager.RemoveFromRolesAsync(user, existingRoles);
            var result = await _userManager.AddToRoleAsync(user, role);

            if (result.Succeeded)
            {
                await _context.AuditLogs.AddAsync(new AuditLog
                {
                    UserId = User.Identity.Name,
                    Action = "UpdateRole",
                    Details = $"Cập nhật quyền của {user.Email} thành {role}",
                    Timestamp = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();
                
                return Json(new { success = true, message = $"Đã cập nhật quyền thành {role}" });
            }

            return Json(new { success = false, message = "Không thể cập nhật quyền" });
        }

        [HttpPost]
        public async Task<IActionResult> RestrictUser(string userName, string restrictionType, string reason, DateTime? endTime, string notes)
        {
            var user = await _userManager.FindByNameAsync(userName);
            if (user == null)
            {
                return Json(new { success = false, message = "Không tìm thấy người dùng" });
            }

            // Không cho phép hạn chế tài khoản Admin
            if (await _userManager.IsInRoleAsync(user, "Admin"))
            {
                return Json(new { success = false, message = "Không thể hạn chế tài khoản Admin" });
            }

            var adminUser = await _userManager.GetUserAsync(User);
            if (adminUser == null)
            {
                return Json(new { success = false, message = "Không xác định được người quản trị" });
            }

            var restriction = new AccountRestriction
            {
                UserId = user.Id,
                RestrictedBy = adminUser.Id,
                RestrictionType = restrictionType,
                Reason = reason,
                StartTime = DateTimeOffset.Now,
                EndTime = endTime.HasValue ? new DateTimeOffset(endTime.Value) : null,
                IsActive = true,
                Notes = notes ?? ""
            };

            // Vô hiệu hóa tài khoản nếu là Disable
            if (restrictionType == "Disable")
            {
                user.IsActive = false;
                await _userManager.UpdateAsync(user);
            }

            await _context.AccountRestrictions.AddAsync(restriction);
            
            // Ghi log
            await _context.AuditLogs.AddAsync(new AuditLog
            {
                UserId = adminUser.UserName,
                Action = "RestrictUser",
                Details = $"Hạn chế tài khoản {user.UserName} - Loại: {restrictionType}. Lý do: {reason}",
                Timestamp = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();

            return Json(new { 
                success = true, 
                message = $"Đã áp dụng hạn chế {restrictionType} cho tài khoản {user.UserName}" 
            });
        }
    }
}
