using System.ComponentModel.DataAnnotations;

namespace QotD.Bot.Data.Models;

public sealed class TeamLeaveEntry
{
    public int Id { get; set; }

    [Required]
    public ulong GuildId { get; set; }

    [Required]
    public ulong UserId { get; set; }

    [Required]
    [MaxLength(500)]
    public string Reason { get; set; } = string.Empty;

    public DateTimeOffset StartUtc { get; set; }

    public DateTimeOffset? EndUtc { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}