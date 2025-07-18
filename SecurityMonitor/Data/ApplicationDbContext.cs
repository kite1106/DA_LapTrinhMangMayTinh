using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SecurityMonitor.Models;

namespace SecurityMonitor.Data;

/// <summary>
/// DbContext chính của ứng dụng
/// </summary>
public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    // Identity
    public DbSet<ApplicationUser> Users { get; set; } = null!;
    
    // Entities hệ thống giám sát
    public DbSet<AlertType> AlertTypes { get; set; } = null!;
    public DbSet<SeverityLevel> SeverityLevels { get; set; } = null!;
    public DbSet<AlertStatus> AlertStatuses { get; set; } = null!;
    public DbSet<LogSource> LogSources { get; set; } = null!;
    public DbSet<Log> Logs { get; set; } = null!;
    public DbSet<Alert> Alerts { get; set; } = null!;
    public DbSet<AuditLog> AuditLogs { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Quan hệ cảnh báo với người dùng được giao
        builder.Entity<Alert>()
            .HasOne(a => a.AssignedTo)
            .WithMany(u => u.AssignedAlerts)
            .HasForeignKey(a => a.AssignedToId)
            .OnDelete(DeleteBehavior.NoAction); // ✅ Tránh multiple cascade

        // Quan hệ người dùng xử lý cảnh báo
        builder.Entity<Alert>()
            .HasOne(a => a.ResolvedBy)
            .WithMany(u => u.ResolvedAlerts)
            .HasForeignKey(a => a.ResolvedById)
            .OnDelete(DeleteBehavior.SetNull); // ✅ An toàn khi xóa người dùng

        // Index cho các trường thời gian
        builder.Entity<Log>()
            .HasIndex(l => l.Timestamp);

        builder.Entity<Alert>()
            .HasIndex(a => a.Timestamp);

        builder.Entity<AuditLog>()
            .HasIndex(a => a.Timestamp);

        // Index cho trạng thái và mức độ nghiêm trọng
        builder.Entity<Alert>()
            .HasIndex(a => a.StatusId);

        builder.Entity<Alert>()
            .HasIndex(a => a.SeverityLevelId);
    }
}
