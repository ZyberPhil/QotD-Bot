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

    /// <summary>
    /// Minimum number of active human users in the same voice channel to grant voice XP.
    /// </summary>
    public int VoiceMinActiveUsers { get; set; } = 2;

    /// <summary>
    /// Whether users who are self-muted or self-deafened are eligible for voice XP.
    /// </summary>
    public bool VoiceAllowSelfMutedOrDeafened { get; set; } = false;
}
