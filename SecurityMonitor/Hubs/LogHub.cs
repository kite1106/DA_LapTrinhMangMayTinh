using Microsoft.AspNetCore.SignalR;
using SecurityMonitor.Models;

namespace SecurityMonitor.Hubs
{
    public class LogHub : Hub
    {
        public async Task JoinLogGroup()
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, "LogGroup");
        }

        public async Task LeaveLogGroup()
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, "LogGroup");
        }

        public async Task SendNewLog(LogEntry log)
        {
            await Clients.Group("LogGroup").SendAsync("ReceiveNewLog", log);
        }

        public async Task SendLogUpdate(int logId, string action)
        {
            await Clients.Group("LogGroup").SendAsync("ReceiveLogUpdate", logId, action);
        }
    }
} 