using System.ComponentModel.DataAnnotations;

namespace QotD.Bot.Features.Leveling.Data.Models;

/// <summary>
/// Guild-specific configuration for the leveling system.
/// </summary>
public sealed class LevelingConfig
{
    public int Id { get; set; }

    [Required]
    public long GuildId { get; set; }

    /// <summary>
    /// Channel ID where level-up messages are posted (0 = disabled).
    /// </summary>
    public long LevelUpChannelId { get; set; }

    public bool IsEnabled { get; set; } = true;
}
