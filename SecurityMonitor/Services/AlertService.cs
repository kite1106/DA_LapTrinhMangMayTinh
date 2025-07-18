using Microsoft.EntityFrameworkCore;
using SecurityMonitor.Data;
using SecurityMonitor.Models;

namespace SecurityMonitor.Services;

public class AlertService : IAlertService
{
    private readonly ApplicationDbContext _context;
    private readonly IAuditService _auditService;
    private readonly ILogger<AlertService> _logger;

    public AlertService(
        ApplicationDbContext context,
        IAuditService auditService,
        ILogger<AlertService> logger)
    {
        _context = context;
        _auditService = auditService;
        _logger = logger;
    }

    public async Task<IEnumerable<Alert>> GetAllAlertsAsync()
    {
        return await _context.Alerts
            .Include(a => a.AlertType)
            .Include(a => a.SeverityLevel)
            .Include(a => a.Status)
            .Include(a => a.AssignedTo)
            .OrderByDescending(a => a.Timestamp)
            .ToListAsync();
    }

    public async Task<Alert?> GetAlertByIdAsync(int id)
    {
        return await _context.Alerts
            .Include(a => a.AlertType)
            .Include(a => a.SeverityLevel)
            .Include(a => a.Status)
            .Include(a => a.AssignedTo)
            .FirstOrDefaultAsync(a => a.Id == id);
    }

    public async Task<Alert> CreateAlertAsync(Alert alert)
    {
        alert.Timestamp = DateTime.Now;
        _context.Alerts.Add(alert);
        await _context.SaveChangesAsync();

        await _auditService.LogActivityAsync(
            alert.AssignedToId ?? "System",
            "Create",
            "Alert",
            alert.Id.ToString(),
            $"Created alert: {alert.Title}"
        );

        return alert;
    }

    public async Task<Alert?> UpdateAlertAsync(int id, Alert alert)
    {
        var existingAlert = await _context.Alerts.FindAsync(id);
        if (existingAlert == null) return null;

        existingAlert.Title = alert.Title;
        existingAlert.Description = alert.Description;
        existingAlert.AlertTypeId = alert.AlertTypeId;
        existingAlert.SeverityLevelId = alert.SeverityLevelId;
        existingAlert.StatusId = alert.StatusId;

        await _context.SaveChangesAsync();

        await _auditService.LogActivityAsync(
            alert.AssignedToId ?? "System",
            "Update",
            "Alert",
            alert.Id.ToString(),
            $"Updated alert: {alert.Title}"
        );

        return existingAlert;
    }

    public async Task<bool> DeleteAlertAsync(int id)
    {
        var alert = await _context.Alerts.FindAsync(id);
        if (alert == null) return false;

        _context.Alerts.Remove(alert);
        await _context.SaveChangesAsync();

        await _auditService.LogActivityAsync(
            "System",
            "Delete",
            "Alert",
            id.ToString(),
            $"Deleted alert: {alert.Title}"
        );

        return true;
    }

    public async Task<IEnumerable<Alert>> GetAlertsBySeverityAsync(int severityLevelId)
    {
        return await _context.Alerts
            .Where(a => a.SeverityLevelId == severityLevelId)
            .Include(a => a.AlertType)
            .Include(a => a.Status)
            .OrderByDescending(a => a.Timestamp)
            .ToListAsync();
    }

    public async Task<IEnumerable<Alert>> GetAlertsByStatusAsync(int statusId)
    {
        return await _context.Alerts
            .Where(a => a.StatusId == statusId)
            .Include(a => a.AlertType)
            .Include(a => a.SeverityLevel)
            .OrderByDescending(a => a.Timestamp)
            .ToListAsync();
    }

    public async Task<Alert?> AssignAlertAsync(int alertId, string userId)
    {
        var alert = await _context.Alerts.FindAsync(alertId);
        if (alert == null) return null;

        alert.AssignedToId = userId;
        await _context.SaveChangesAsync();

        await _auditService.LogActivityAsync(
            userId,
            "Assign",
            "Alert",
            alertId.ToString(),
            $"Alert assigned to user"
        );

        return alert;
    }

    public async Task<Alert?> ResolveAlertAsync(int alertId, string userId, string resolution)
    {
        var alert = await _context.Alerts.FindAsync(alertId);
        if (alert == null) return null;

        alert.ResolvedById = userId;
        alert.ResolvedAt = DateTime.Now;
        alert.Resolution = resolution;
        alert.StatusId = (await _context.AlertStatuses.FirstAsync(s => s.IsTerminal)).Id;

        await _context.SaveChangesAsync();

        await _auditService.LogActivityAsync(
            userId,
            "Resolve",
            "Alert",
            alertId.ToString(),
            $"Alert resolved with resolution: {resolution}"
        );

        return alert;
    }
}
