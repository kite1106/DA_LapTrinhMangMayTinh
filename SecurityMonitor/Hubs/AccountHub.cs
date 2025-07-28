using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using SecurityMonitor.Data;
using SecurityMonitor.Models;
using System;
using System.Threading.Tasks;

namespace SecurityMonitor.Hubs
{
    public class AccountHub : Hub
    {
        private readonly ILogger<AccountHub> _logger;
        private readonly ApplicationDbContext _context;

        public AccountHub(ILogger<AccountHub> logger, ApplicationDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        public async Task SendUserStatusUpdate(string userId, string userName, bool isLocked)
        {
            try
            {
                await Clients.All.SendAsync("UserStatusUpdated", userName, isLocked, userId);
                _logger.LogInformation("User status update sent for {UserName} (ID: {UserId}). Status: {Status}", 
                    userName, userId, isLocked ? "Locked" : "Unlocked");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending user status update for {UserName}", userName);
                throw;
            }
        }

        public async Task SendUserRestricted(string userId, string userName, string reason)
        {
            try
            {
                await Clients.All.SendAsync("UserRestricted", userName, reason, userId);
                _logger.LogInformation("User restriction notification sent for {UserName} (ID: {UserId}). Reason: {Reason}", 
                    userName, userId, reason);

                // Log the restriction event
                var restrictionEvent = new AccountRestrictionEvent
                {
                    UserId = userId,
                    UserName = userName,
                    RestrictionReason = reason,
                    Timestamp = DateTimeOffset.UtcNow
                };
                _context.AccountRestrictionEvents.Add(restrictionEvent);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending user restriction notification for {UserName}", userName);
                throw;
            }
        }

        public async Task SendUserUnrestricted(string userId, string userName)
        {
            try
            {
                await Clients.All.SendAsync("UserUnrestricted", userName, userId);
                _logger.LogInformation("User unrestriction notification sent for {UserName} (ID: {UserId})", 
                    userName, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending user unrestriction notification for {UserName}", userName);
                throw;
            }
        }

        public override async Task OnConnectedAsync()
        {
            try
            {
                await base.OnConnectedAsync();
                var connectionId = Context.ConnectionId;
                _logger.LogInformation("New SignalR connection established. Connection ID: {ConnectionId}", connectionId);
                await Clients.Caller.SendAsync("ConnectionEstablished", connectionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in OnConnectedAsync");
                throw;
            }
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            try
            {
                if (exception != null)
                {
                    _logger.LogWarning(exception, "Client disconnected with error. Connection ID: {ConnectionId}", 
                        Context.ConnectionId);
                }
                else
                {
                    _logger.LogInformation("Client disconnected normally. Connection ID: {ConnectionId}", 
                        Context.ConnectionId);
                }
                await base.OnDisconnectedAsync(exception);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in OnDisconnectedAsync");
                throw;
            }
        }
    }
}
