using System.ComponentModel.DataAnnotations;

namespace QotD.Bot.Data.Models;

public sealed class TeamActivityPolicy
{
    public int Id { get; set; }

    [Required]
    public ulong GuildId { get; set; }

    [Required]
    public ulong RoleId { get; set; }

    public int MinMessagesPerWeek { get; set; } = 0;

    public int MinVoiceMinutesPerWeek { get; set; } = 0;

    public bool IsEnabled { get; set; } = true;

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}