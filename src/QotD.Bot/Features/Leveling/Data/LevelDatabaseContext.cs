using Microsoft.EntityFrameworkCore;
using QotD.Bot.Features.Leveling.Data.Models;

namespace QotD.Bot.Features.Leveling.Data;

public sealed class LevelDatabaseContext : DbContext
{
    public LevelDatabaseContext(DbContextOptions<LevelDatabaseContext> options) : base(options)
    {
    }

    public DbSet<LevelUserStats> LevelUserStats => Set<LevelUserStats>();
    public DbSet<LevelingConfig> LevelingConfigs => Set<LevelingConfig>();
    public DbSet<LevelActivityLog> LevelActivityLogs => Set<LevelActivityLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<LevelUserStats>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.Property(x => x.UserId).IsRequired();
            entity.Property(x => x.GuildId).IsRequired();
            entity.Property(x => x.XP).HasDefaultValue(0);
            entity.Property(x => x.Level).HasDefaultValue(0);
            entity.Property(x => x.MessageCount).HasDefaultValue(0);

            entity.HasIndex(x => new { x.GuildId, x.UserId }).IsUnique();
            entity.HasIndex(x => new { x.GuildId, x.Level, x.XP });
        });

        modelBuilder.Entity<LevelingConfig>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.Property(x => x.GuildId).IsRequired();
            entity.Property(x => x.LevelUpChannelId).HasDefaultValue(0);
            entity.Property(x => x.LevelUpBannerUrl).HasMaxLength(2048);
            entity.Property(x => x.IsEnabled).HasDefaultValue(true);
            entity.Property(x => x.VoiceMinActiveUsers).HasDefaultValue(2);
            entity.Property(x => x.VoiceAllowSelfMutedOrDeafened).HasDefaultValue(false);

            entity.HasIndex(x => x.GuildId).IsUnique();
        });

        modelBuilder.Entity<LevelActivityLog>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ActivityType).HasConversion<int>();
            entity.Property(x => x.Amount).HasDefaultValue(1);
            entity.HasIndex(x => new { x.GuildId, x.UserId, x.OccurredAtUtc });
            entity.HasIndex(x => new { x.GuildId, x.OccurredAtUtc, x.ActivityType });
        });
    }
}
