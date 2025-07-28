using Microsoft.AspNetCore.Identity;
using SecurityMonitor.Models;
using SecurityMonitor.DTOs;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SecurityMonitor.Services.Interfaces
{
    public interface IUserCacheService
    {
        Task<List<UserManagementDto>> GetAllUsersAsync();
        Task<ApplicationUser?> GetUserAsync(string userId, bool trackChanges = false);
        Task<bool> UpdateUserAsync(ApplicationUser user);
        Task<IList<string>> GetUserRolesAsync(ApplicationUser user);
    }
}
