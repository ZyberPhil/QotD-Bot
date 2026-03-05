using Microsoft.EntityFrameworkCore;
using QotD.Bot.Data.Models;

namespace QotD.Bot.Data;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Question> Questions => Set<Question>();

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
    }
}
