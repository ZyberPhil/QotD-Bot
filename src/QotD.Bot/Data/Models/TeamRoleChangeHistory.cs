using System.ComponentModel.DataAnnotations;

namespace QotD.Bot.Data.Models;

public sealed class TeamRoleChangeHistory
{
    public int Id { get; set; }

    [Required]
    public ulong GuildId { get; set; }

    [Required]
    public ulong UserId { get; set; }

    public ulong? OldRoleId { get; set; }

    [MaxLength(100)]
    public string? OldRoleName { get; set; }

    public ulong? NewRoleId { get; set; }

    [MaxLength(100)]
    public string? NewRoleName { get; set; }

    public DateTimeOffset ChangedAtUtc { get; set; }

    [MaxLength(250)]
    public string? ChangeReason { get; set; }
}