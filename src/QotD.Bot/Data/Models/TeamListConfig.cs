using System.ComponentModel.DataAnnotations;

namespace QotD.Bot.Data.Models;

public sealed class TeamListConfig
{
    [Key]
    public ulong GuildId { get; set; }

    public ulong? ChannelId { get; set; }

    public ulong? MessageId { get; set; }

    public ulong[] TrackedRoles { get; set; } = Array.Empty<ulong>();

    public string? CustomTemplate { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
