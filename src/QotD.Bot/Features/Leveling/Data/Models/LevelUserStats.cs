using System.ComponentModel.DataAnnotations;

namespace QotD.Bot.Features.Leveling.Data.Models;

public sealed class LevelUserStats
{
    public int Id { get; set; }

    [Required]
    public long UserId { get; set; }

    [Required]
    public long GuildId { get; set; }

    public int XP { get; set; }

    public int Level { get; set; }

    public int MessageCount { get; set; }

    public int VoiceMinutes { get; set; }

    public DateTimeOffset? LastMessageXpAtUtc { get; set; }

    public DateTimeOffset? LastVoiceXpAtUtc { get; set; }
}
