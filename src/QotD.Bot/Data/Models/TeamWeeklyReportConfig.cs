using System.ComponentModel.DataAnnotations;

namespace QotD.Bot.Data.Models;

public sealed class TeamWeeklyReportConfig
{
    [Key]
    public ulong GuildId { get; set; }

    public ulong? ChannelId { get; set; }

    public bool IsEnabled { get; set; } = false;

    public DateTimeOffset? LastReportedWeekStartUtc { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}