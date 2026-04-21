using System.ComponentModel.DataAnnotations;

namespace QotD.Bot.Data.Models;

public sealed class GuildIpBanEntry
{
    public int Id { get; set; }

    [Required]
    public ulong GuildId { get; set; }

    [Required]
    [MaxLength(64)]
    public string IpHash { get; set; } = string.Empty;

    [Required]
    [MaxLength(64)]
    public string MaskedIp { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Note { get; set; }

    [Required]
    public ulong CreatedByUserId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
