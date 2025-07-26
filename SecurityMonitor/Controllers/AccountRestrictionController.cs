using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecurityMonitor.Data;
using SecurityMonitor.DTOs.Security;
using SecurityMonitor.Models;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace SecurityMonitor.Controllers
{
    [Authorize(Roles = "Admin")]
    [Route("api/[controller]")]
    [ApiController]
    public class AccountRestrictionController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<AccountRestrictionController> _logger;

        public AccountRestrictionController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ILogger<AccountRestrictionController> logger)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
        }

        [HttpGet("user/{userId}")]
        public async Task<ActionResult<AccountRestrictionInfoDto>> GetUserRestrictions(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return NotFound("Người dùng không tồn tại");
            }

            var restriction = await _context.AccountRestrictions
                .Include(r => r.User)
                .Include(r => r.RestrictedByUser)
                .Where(r => r.UserId == userId && r.IsActive)
                .OrderByDescending(r => r.StartTime)
                .FirstOrDefaultAsync();

            if (restriction == null)
            {
                return NotFound("Không có hạn chế nào được áp dụng");
            }

            return new AccountRestrictionInfoDto
            {
                Id = restriction.Id,
                UserId = restriction.UserId,
                UserName = restriction.User.UserName ?? "",
                FullName = restriction.User.FullName,
                RestrictionType = restriction.RestrictionType,
                Reason = restriction.Reason,
                RestrictedBy = restriction.RestrictedBy,
                RestrictedByName = restriction.RestrictedByUser?.UserName ?? "",
                StartTime = restriction.StartTime,
                EndTime = restriction.EndTime,
                IsActive = restriction.IsActive,
                Notes = restriction.Notes
            };
        }

        [HttpPost]
        public async Task<IActionResult> CreateRestriction(AccountRestrictionDto dto)
        {
            var user = await _userManager.FindByIdAsync(dto.UserId);
            if (user == null)
            {
                return NotFound("Người dùng không tồn tại");
            }

            // Lấy thông tin admin thực hiện hành động
            var adminId = _userManager.GetUserId(User);
            if (adminId == null)
            {
                return Unauthorized("Không tìm thấy thông tin người quản trị");
            }

            // Kiểm tra xem có hạn chế đang hoạt động không
            var existingRestriction = await _context.AccountRestrictions
                .Where(r => r.UserId == dto.UserId && r.IsActive)
                .FirstOrDefaultAsync();

            if (existingRestriction != null)
            {
                // Vô hiệu hóa hạn chế cũ
                existingRestriction.IsActive = false;
                existingRestriction.EndTime = DateTimeOffset.Now;
            }

            // Tạo hạn chế mới
            var restriction = new AccountRestriction
            {
                UserId = dto.UserId,
                RestrictedBy = adminId,
                RestrictionType = dto.RestrictionType,
                Reason = dto.Reason,
                StartTime = DateTimeOffset.Now,
                EndTime = dto.EndTime,
                IsActive = true,
                Notes = dto.Notes ?? ""
            };

            _context.AccountRestrictions.Add(restriction);

            // Cập nhật trạng thái người dùng
            switch (dto.RestrictionType.ToLower())
            {
                case "disable":
                    user.IsActive = false;
                    break;
                case "readonly":
                    // Thêm logic xử lý readonly nếu cần
                    break;
            }

            await _context.SaveChangesAsync();
            
            // Log hành động
            _logger.LogInformation(
                "Admin {AdminId} đã áp dụng hạn chế {RestrictionType} cho user {UserId}. Lý do: {Reason}",
                adminId, dto.RestrictionType, dto.UserId, dto.Reason);

            return Ok(new { Message = "Đã áp dụng hạn chế thành công" });
        }

        [HttpPost("{id}/remove")]
        public async Task<IActionResult> RemoveRestriction(int id)
        {
            var restriction = await _context.AccountRestrictions
                .Include(r => r.User)
                .FirstOrDefaultAsync(r => r.Id == id && r.IsActive);

            if (restriction == null)
            {
                return NotFound("Không tìm thấy hạn chế hoặc hạn chế đã bị vô hiệu hóa");
            }

            var adminId = _userManager.GetUserId(User);
            if (adminId == null)
            {
                return Unauthorized("Không tìm thấy thông tin người quản trị");
            }

            // Vô hiệu hóa hạn chế
            restriction.IsActive = false;
            restriction.EndTime = DateTimeOffset.Now;

            // Khôi phục trạng thái người dùng nếu bị disable
            if (restriction.RestrictionType.ToLower() == "disable")
            {
                restriction.User.IsActive = true;
            }

            await _context.SaveChangesAsync();

            // Log hành động
            _logger.LogInformation(
                "Admin {AdminId} đã gỡ bỏ hạn chế cho user {UserId}",
                adminId, restriction.UserId);

            return Ok(new { Message = "Đã gỡ bỏ hạn chế thành công" });
        }

        [HttpGet("list")]
        public async Task<ActionResult<IEnumerable<AccountRestrictionInfoDto>>> GetActiveRestrictions()
        {
            var restrictions = await _context.AccountRestrictions
                .Include(r => r.User)
                .Include(r => r.RestrictedByUser)
                .Where(r => r.IsActive)
                .OrderByDescending(r => r.StartTime)
                .Select(r => new AccountRestrictionInfoDto
                {
                    Id = r.Id,
                    UserId = r.UserId,
                    UserName = r.User.UserName ?? "",
                    FullName = r.User.FullName,
                    RestrictionType = r.RestrictionType,
                    Reason = r.Reason,
                    RestrictedBy = r.RestrictedBy,
                    RestrictedByName = r.RestrictedByUser.UserName ?? "",
                    StartTime = r.StartTime,
                    EndTime = r.EndTime,
                    IsActive = r.IsActive,
                    Notes = r.Notes
                })
                .ToListAsync();

            return Ok(restrictions);
        }
    }
}
