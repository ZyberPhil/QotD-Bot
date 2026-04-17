using Microsoft.EntityFrameworkCore;
using QotD.Bot.Data.Models;

namespace QotD.Bot.Data;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Question> Questions => Set<Question>();
    public DbSet<GuildConfig> GuildConfigs => Set<GuildConfig>();
    public DbSet<GuildHistory> GuildHistories => Set<GuildHistory>();
    
    // MiniGames
    public DbSet<CountingChannelConfig> CountingChannels => Set<CountingChannelConfig>();
    public DbSet<WordChainConfig> WordChainConfigs => Set<WordChainConfig>();

    // Logging
    public DbSet<LogRoutingConfig> LogRoutingConfigs => Set<LogRoutingConfig>();

    // Link Moderation
    public DbSet<LinkFilterConfig> LinkFilterConfigs => Set<LinkFilterConfig>();
    public DbSet<LinkFilterRule> LinkFilterRules => Set<LinkFilterRule>();
    public DbSet<LinkFilterBypassRole> LinkFilterBypassRoles => Set<LinkFilterBypassRole>();
    public DbSet<LinkFilterBypassChannel> LinkFilterBypassChannels => Set<LinkFilterBypassChannel>();

    // Auto Moderation
    public DbSet<AutoModerationConfig> AutoModerationConfigs => Set<AutoModerationConfig>();
    public DbSet<AutoModerationRaidIncident> AutoModerationRaidIncidents => Set<AutoModerationRaidIncident>();
    public DbSet<AutoModerationAuditEntry> AutoModerationAuditEntries => Set<AutoModerationAuditEntry>();

    // Teams
    public DbSet<TeamListConfig> TeamListConfigs => Set<TeamListConfig>();
    public DbSet<TeamActivityPolicy> TeamActivityPolicies => Set<TeamActivityPolicy>();
    public DbSet<TeamActivityWeeklySnapshot> TeamActivityWeeklySnapshots => Set<TeamActivityWeeklySnapshot>();
    public DbSet<TeamWeeklyReportConfig> TeamWeeklyReportConfigs => Set<TeamWeeklyReportConfig>();
    public DbSet<TeamRoleChangeHistory> TeamRoleChangeHistories => Set<TeamRoleChangeHistory>();
    public DbSet<TeamWarning> TeamWarnings => Set<TeamWarning>();
    public DbSet<TeamWarningNote> TeamWarningNotes => Set<TeamWarningNote>();
    public DbSet<TeamLeaveEntry> TeamLeaveEntries => Set<TeamLeaveEntry>();

    // Self Roles
    public DbSet<SelfRoleConfig> SelfRoleConfigs => Set<SelfRoleConfig>();
    public DbSet<SelfRoleGroup> SelfRoleGroups => Set<SelfRoleGroup>();
    public DbSet<SelfRoleOption> SelfRoleOptions => Set<SelfRoleOption>();
    public DbSet<SelfRoleRequest> SelfRoleRequests => Set<SelfRoleRequest>();

    // Birthdays
    public DbSet<UserBirthday> UserBirthdays => Set<UserBirthday>();
    public DbSet<BirthdayConfig> BirthdayConfigs => Set<BirthdayConfig>();

    // TempVoice
    public DbSet<TempVoiceConfig> TempVoiceConfigs => Set<TempVoiceConfig>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Question>(entity =>
        {
            entity.HasKey(q => q.Id);

            entity.Property(q => q.QuestionText)
                  .IsRequired()
                  .HasMaxLength(2000);

            entity.Property(q => q.Posted)
                  .HasDefaultValue(false);

            entity.Property(q => q.CreatedAt)
                  .HasDefaultValueSql("now()");

            // Ensure only one question per day
            entity.HasIndex(q => q.ScheduledFor)
                  .IsUnique();
        });

        modelBuilder.Entity<GuildConfig>(entity =>
        {
            entity.HasKey(g => g.GuildId);
            entity.Property(g => g.GuildId).ValueGeneratedNever();
        });

        modelBuilder.Entity<GuildHistory>(entity =>
        {
            entity.HasKey(h => h.Id);
            entity.HasIndex(h => new { h.GuildId, h.QuestionId }).IsUnique();
            
            entity.HasOne(h => h.Question)
                  .WithMany()
                  .HasForeignKey(h => h.QuestionId);
        });

        modelBuilder.Entity<CountingChannelConfig>(entity =>
        {
            entity.HasIndex(c => c.ChannelId).IsUnique();
        });

        modelBuilder.Entity<WordChainConfig>(entity =>
        {
            entity.HasIndex(c => c.ChannelId).IsUnique();
        });

        modelBuilder.Entity<LinkFilterConfig>(entity =>
        {
            entity.HasKey(x => x.GuildId);
            entity.Property(x => x.GuildId).ValueGeneratedNever();
            entity.Property(x => x.Mode).HasConversion<int>();
            entity.HasIndex(x => x.LogChannelId);
        });

        modelBuilder.Entity<LinkFilterRule>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.GuildId, x.NormalizedDomain }).IsUnique();
            entity.HasIndex(x => x.GuildId);
        });

        modelBuilder.Entity<LinkFilterBypassRole>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.GuildId, x.RoleId }).IsUnique();
            entity.HasIndex(x => x.GuildId);
        });

        modelBuilder.Entity<LinkFilterBypassChannel>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.GuildId, x.ChannelId }).IsUnique();
            entity.HasIndex(x => x.GuildId);
        });

        modelBuilder.Entity<AutoModerationConfig>(entity =>
        {
            entity.HasKey(x => x.GuildId);
            entity.Property(x => x.GuildId).ValueGeneratedNever();
            entity.HasIndex(x => x.LogChannelId);
            entity.HasIndex(x => x.IsEnabled);
        });

        modelBuilder.Entity<AutoModerationRaidIncident>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.GuildId);
            entity.HasIndex(x => new { x.GuildId, x.StartedAtUtc });
            entity.HasIndex(x => new { x.GuildId, x.EndedAtUtc });
        });

        modelBuilder.Entity<AutoModerationAuditEntry>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Action).HasConversion<int>();
            entity.HasIndex(x => x.GuildId);
            entity.HasIndex(x => new { x.GuildId, x.CreatedAtUtc });
            entity.HasIndex(x => new { x.GuildId, x.UserId, x.CreatedAtUtc });
            entity.HasIndex(x => new { x.GuildId, x.RuleKey, x.CreatedAtUtc });
        });

        modelBuilder.Entity<UserBirthday>(entity =>
        {
            entity.HasIndex(b => new { b.MemberId, b.GuildId }).IsUnique();
        });

        modelBuilder.Entity<BirthdayConfig>(entity =>
        {
            entity.HasKey(c => c.GuildId);
            entity.Property(c => c.GuildId).ValueGeneratedNever();
        });

        modelBuilder.Entity<TempVoiceConfig>(entity =>
        {
            entity.HasKey(c => c.GuildId);
            entity.Property(c => c.GuildId).ValueGeneratedNever();
        });

        modelBuilder.Entity<TeamActivityPolicy>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.GuildId, x.RoleId }).IsUnique();
            entity.HasIndex(x => x.GuildId);
        });

        modelBuilder.Entity<TeamActivityWeeklySnapshot>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.GuildId, x.WeekStartUtc, x.RoleId, x.UserId }).IsUnique();
            entity.HasIndex(x => new { x.GuildId, x.WeekStartUtc });
            entity.HasIndex(x => new { x.GuildId, x.UserId });
        });

        modelBuilder.Entity<TeamWeeklyReportConfig>(entity =>
        {
            entity.HasKey(x => x.GuildId);
            entity.Property(x => x.GuildId).ValueGeneratedNever();
            entity.HasIndex(x => x.ChannelId);
        });

        modelBuilder.Entity<TeamRoleChangeHistory>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.GuildId, x.UserId, x.ChangedAtUtc });
        });

        modelBuilder.Entity<TeamWarning>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.WarningType).HasConversion<int>();
            entity.HasIndex(x => new { x.GuildId, x.UserId, x.IsActive, x.IsResolved });
            entity.HasIndex(x => new { x.GuildId, x.WeekStartUtc, x.UserId, x.RoleId, x.WarningType });
        });

        modelBuilder.Entity<TeamWarningNote>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.NoteType).HasConversion<int>();
            entity.HasOne(x => x.Warning)
                  .WithMany(w => w.Notes)
                  .HasForeignKey(x => x.WarningId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => new { x.GuildId, x.WarningId, x.CreatedAtUtc });
        });

        modelBuilder.Entity<TeamLeaveEntry>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.GuildId, x.UserId, x.StartUtc });
            entity.HasIndex(x => new { x.GuildId, x.UserId, x.EndUtc });
        });

        modelBuilder.Entity<SelfRoleConfig>(entity =>
        {
            entity.HasKey(x => x.GuildId);
            entity.Property(x => x.GuildId).ValueGeneratedNever();
            entity.HasIndex(x => x.PanelChannelId);
            entity.HasIndex(x => x.ModerationChannelId);
        });

        modelBuilder.Entity<SelfRoleGroup>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.GuildId, x.Name }).IsUnique();
            entity.HasIndex(x => new { x.GuildId, x.DisplayOrder });
        });

        modelBuilder.Entity<SelfRoleOption>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.GuildId, x.RoleId }).IsUnique();
            entity.HasIndex(x => new { x.GuildId, x.EmojiKey }).IsUnique();
            entity.HasIndex(x => new { x.GuildId, x.DisplayOrder });
            entity.HasOne(x => x.Group)
                  .WithMany(g => g.Options)
                  .HasForeignKey(x => x.GroupId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<SelfRoleRequest>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Status).HasConversion<int>();
            entity.HasIndex(x => new { x.GuildId, x.Status, x.RequestedAtUtc });
            entity.HasIndex(x => new { x.GuildId, x.UserId, x.RoleId, x.Status });
        });
    }
}
