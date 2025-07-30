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
    public override DbSet<ApplicationUser> Users { get; set; } = null!;
    
    // Entities hệ thống giám sát
    public DbSet<AlertType> AlertTypes { get; set; } = null!;
    public DbSet<SeverityLevel> SeverityLevels { get; set; } = null!;
    public DbSet<AlertStatus> AlertStatuses { get; set; } = null!;
    public DbSet<LogSource> LogSources { get; set; } = null!;
            public DbSet<LogEntry> Logs { get; set; } = null!;
    public DbSet<Alert> Alerts { get; set; } = null!;
    public DbSet<AuditLog> AuditLogs { get; set; } = null!;
    public DbSet<AccountRestriction> AccountRestrictions { get; set; } = null!;
    public DbSet<AccountRestrictionEvent> AccountRestrictionEvents { get; set; } = null!;
    public DbSet<BlockedIP> BlockedIPs { get; set; } = null!;
    
    // New Log Analysis entities (3NF compliant)
    public DbSet<LogEntry> LogEntries { get; set; } = null!;
    public DbSet<LogLevelType> LogLevelTypes { get; set; } = null!;
    public DbSet<LogComponent> LogComponents { get; set; } = null!;
    public DbSet<LogEventType> LogEventTypes { get; set; } = null!;
    public DbSet<LogSeverity> LogSeverities { get; set; } = null!;
    public DbSet<LogTag> LogTags { get; set; } = null!;
    public DbSet<LogEntryTag> LogEntryTags { get; set; } = null!;
    public DbSet<LogAnalysis> LogAnalyses { get; set; } = null!;
    public DbSet<AlertRule> AlertRules { get; set; } = null!;
    public DbSet<AlertCondition> AlertConditions { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Account restrictions
        builder.Entity<AccountRestriction>()
            .HasOne(ar => ar.User)
            .WithMany()
            .HasForeignKey(ar => ar.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<AccountRestriction>()
            .HasOne(ar => ar.RestrictedByUser)
            .WithMany()
            .HasForeignKey(ar => ar.RestrictedBy)
            .OnDelete(DeleteBehavior.Restrict);

        // Account restriction events
        builder.Entity<AccountRestrictionEvent>()
            .HasOne(are => are.User)
            .WithMany()
            .HasForeignKey(are => are.UserId)
            .OnDelete(DeleteBehavior.Restrict);
            
        builder.Entity<AccountRestrictionEvent>()
            .HasIndex(are => are.Timestamp);

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

        builder.Entity<LogEntry>()
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

        // Index cho Log Analysis
        builder.Entity<LogEntry>()
            .HasIndex(l => l.LogLevelTypeId);

        builder.Entity<LogEntry>()
            .HasIndex(l => l.LogSeverityId);

        builder.Entity<LogEntry>()
            .HasIndex(l => l.IpAddress);

        builder.Entity<LogEntry>()
            .HasIndex(l => l.UserId);

        // Many-to-Many relationship for LogEntry and LogTag
        builder.Entity<LogEntryTag>()
            .HasKey(let => new { let.LogEntryId, let.LogTagId });

        builder.Entity<LogEntryTag>()
            .HasOne(let => let.LogEntry)
            .WithMany(le => le.LogEntryTags)
            .HasForeignKey(let => let.LogEntryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<LogEntryTag>()
            .HasOne(let => let.LogTag)
            .WithMany(lt => lt.LogEntryTags)
            .HasForeignKey(let => let.LogTagId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
