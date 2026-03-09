using System.ComponentModel.DataAnnotations;

namespace QotD.Bot.Data.Models;

/// <summary>
/// Per-guild configuration for the Question of the Day.
/// </summary>
public sealed class GuildConfig
{
    [Key]
    public ulong GuildId { get; set; }

    public ulong ChannelId { get; set; }

    public TimeOnly PostTime { get; set; } = new(07, 00);

    public string Timezone { get; set; } = "Europe/Berlin";

    public string? MessageTemplate { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
