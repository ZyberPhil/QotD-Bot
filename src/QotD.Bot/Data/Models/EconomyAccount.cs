using System.ComponentModel.DataAnnotations;

namespace QotD.Bot.Data.Models;

public sealed class EconomyAccount
{
    [Key]
    public ulong UserId { get; set; }

    public long Balance { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}