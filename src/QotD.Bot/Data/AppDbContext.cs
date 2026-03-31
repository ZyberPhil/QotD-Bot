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

    // Teams
    public DbSet<TeamListConfig> TeamListConfigs => Set<TeamListConfig>();

    // Birthdays
    public DbSet<UserBirthday> UserBirthdays => Set<UserBirthday>();
    public DbSet<BirthdayConfig> BirthdayConfigs => Set<BirthdayConfig>();

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

        modelBuilder.Entity<UserBirthday>(entity =>
        {
            entity.HasIndex(b => new { b.MemberId, b.GuildId }).IsUnique();
        });

        modelBuilder.Entity<BirthdayConfig>(entity =>
        {
            entity.HasKey(c => c.GuildId);
            entity.Property(c => c.GuildId).ValueGeneratedNever();
        });
    }
}
