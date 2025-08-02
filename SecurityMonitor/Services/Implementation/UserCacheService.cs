using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SecurityMonitor.Data;
using SecurityMonitor.Models;
using SecurityMonitor.DTOs;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;

using SecurityMonitor.Services.Interfaces;

namespace SecurityMonitor.Services.Implementation
{
    public class UserCacheService : IUserCacheService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks;
        private static readonly SemaphoreSlim _globalLock = new SemaphoreSlim(1, 1);

        public UserCacheService(
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context)
        {
            _userManager = userManager;
            _context = context;
            _locks = new ConcurrentDictionary<string, SemaphoreSlim>();
        }

        private SemaphoreSlim GetUserLock(string userId)
        {
            return _locks.GetOrAdd(userId, _ => new SemaphoreSlim(1, 1));
        }

        public async Task<List<UserManagementDto>> GetAllUsersAsync()
        {
            await _globalLock.WaitAsync();
            try
            {
                var userDtos = new List<UserManagementDto>();
                var users = await _userManager.Users
                    .AsNoTracking()
                    .ToListAsync();

                foreach (var user in users)
                {
                    try
                    {
                        var userLock = GetUserLock(user.Id);
                        await userLock.WaitAsync();
                        try
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
                        finally
                        {
                            userLock.Release();
                        }
                    }
                    catch (Exception)
                    {
                        continue;
                    }
                }

                return userDtos;
            }
            finally
            {
                _globalLock.Release();
            }
        }

        public async Task<ApplicationUser?> GetUserAsync(string userId, bool trackChanges = false)
        {
            var userLock = GetUserLock(userId);
            await userLock.WaitAsync();
            try
            {
                var query = _userManager.Users.Where(u => u.Id == userId);
                if (!trackChanges)
                {
                    query = query.AsNoTracking();
                }
                return await query.FirstOrDefaultAsync();
            }
            finally
            {
                userLock.Release();
            }
        }

        public async Task<bool> UpdateUserAsync(ApplicationUser user)
        {
            var userLock = GetUserLock(user.Id);
            await userLock.WaitAsync();
            try
            {
                var result = await _userManager.UpdateAsync(user);
                return result.Succeeded;
            }
            finally
            {
                userLock.Release();
            }
        }

        public async Task<IList<string>> GetUserRolesAsync(ApplicationUser user)
        {
            var userLock = GetUserLock(user.Id);
            await userLock.WaitAsync();
            try
            {
                return await _userManager.GetRolesAsync(user);
            }
            finally
            {
                userLock.Release();
            }
        }
    }
}
