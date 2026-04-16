using System.ComponentModel.DataAnnotations;

namespace QotD.Bot.Data.Models;

public sealed class SelfRoleOption
{
    [Key]
    public int Id { get; set; }

    public ulong GuildId { get; set; }
    public ulong RoleId { get; set; }

    [MaxLength(128)]
    public string EmojiKey { get; set; } = string.Empty;

    [MaxLength(128)]
    public string Label { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public int DisplayOrder { get; set; }
    public bool RequiresApproval { get; set; }

    public int? GroupId { get; set; }
    public SelfRoleGroup? Group { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}