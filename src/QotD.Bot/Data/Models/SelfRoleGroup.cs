using System.ComponentModel.DataAnnotations;

namespace QotD.Bot.Data.Models;

public sealed class SelfRoleGroup
{
    [Key]
    public int Id { get; set; }

    public ulong GuildId { get; set; }

    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    public bool IsExclusive { get; set; } = true;
    public int DisplayOrder { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<SelfRoleOption> Options { get; set; } = [];
}