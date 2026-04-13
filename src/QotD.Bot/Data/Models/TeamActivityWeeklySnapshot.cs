using System.ComponentModel.DataAnnotations;

namespace QotD.Bot.Data.Models;

public sealed class TeamActivityWeeklySnapshot
{
    public int Id { get; set; }

    [Required]
    public ulong GuildId { get; set; }

    [Required]
    public ulong UserId { get; set; }

    [Required]
    public ulong RoleId { get; set; }

    public DateTimeOffset WeekStartUtc { get; set; }

    public int Messages { get; set; }

    public int VoiceMinutes { get; set; }

    public double CombinedScore { get; set; }

    public bool MeetsMinimum { get; set; }

    public bool WasExcused { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}