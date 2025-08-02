using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SecurityMonitor.Data;
using SecurityMonitor.Models;
using SecurityMonitor.DTOs;
using SecurityMonitor.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;

namespace SecurityMonitor.Services.Implementation
{
    public class UserCacheManager : IUserCacheService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;
        private readonly IMemoryCache _cache;
        private const int CACHE_MINUTES = 5;

        public UserCacheManager(
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context,
            IMemoryCache cache)
        {
            _userManager = userManager;
            _context = context;
            _cache = cache;
        }

        public async Task<List<UserManagementDto>> GetAllUsersAsync()
        {
            const string cacheKey = "AllUsers";
            
            if (_cache.TryGetValue(cacheKey, out List<UserManagementDto> cachedUsers))
            {
                return cachedUsers;
            }

            var users = await _userManager.Users
                .AsNoTracking()
                .ToListAsync();

            var userDtos = new List<UserManagementDto>();

            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                userDtos.Add(new UserManagementDto
                {
                    Id = user.Id,
                    Email = user.Email ?? "",
                    UserName = user.UserName ?? "",
                    IsLocked = user.LockoutEnd != null && user.LockoutEnd > DateTimeOffset.UtcNow,
                    LockoutEnd = user.LockoutEnd,
                    LastLoginTime = user.LastLoginTime,
                    FailedAccessCount = user.AccessFailedCount,
                    Roles = roles
                });
            }

            var cacheOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(TimeSpan.FromMinutes(CACHE_MINUTES));

            _cache.Set(cacheKey, userDtos, cacheOptions);

            return userDtos;
        }

        public async Task<ApplicationUser?> GetUserAsync(string userId, bool trackChanges = false)
        {
            var cacheKey = $"User_{userId}";
            
            if (_cache.TryGetValue(cacheKey, out ApplicationUser cachedUser))
            {
                return cachedUser;
            }

            var query = _userManager.Users.Where(u => u.Id == userId);
            if (!trackChanges)
            {
                query = query.AsNoTracking();
            }

            var user = await query.FirstOrDefaultAsync();
            if (user != null)
            {
                var cacheOptions = new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(TimeSpan.FromMinutes(CACHE_MINUTES));
                _cache.Set(cacheKey, user, cacheOptions);
            }

            return user;
        }

        public async Task<bool> UpdateUserAsync(ApplicationUser user)
        {
            var result = await _userManager.UpdateAsync(user);
            if (result.Succeeded)
            {
                // Invalidate cache
                var cacheKey = $"User_{user.Id}";
                _cache.Remove(cacheKey);
                _cache.Remove("AllUsers");
                return true;
            }
            return false;
        }

        public async Task<IList<string>> GetUserRolesAsync(ApplicationUser user)
        {
            var cacheKey = $"UserRoles_{user.Id}";
            
            if (_cache.TryGetValue(cacheKey, out IList<string> cachedRoles))
            {
                return cachedRoles;
            }

            var roles = await _userManager.GetRolesAsync(user);

            var cacheOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(TimeSpan.FromMinutes(CACHE_MINUTES));
            _cache.Set(cacheKey, roles, cacheOptions);

            return roles;
        }
    }
}
