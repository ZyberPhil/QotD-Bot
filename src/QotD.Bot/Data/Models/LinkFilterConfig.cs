using System.ComponentModel.DataAnnotations;

namespace QotD.Bot.Data.Models;

public enum LinkFilterMode
{
    Blacklist = 0,
    Whitelist = 1
}

public sealed class LinkFilterConfig
{
    [Key]
    public ulong GuildId { get; set; }

    public bool IsEnabled { get; set; } = false;
    public LinkFilterMode Mode { get; set; } = LinkFilterMode.Whitelist;

    public ulong? LogChannelId { get; set; }

    public bool SendDirectMessageWarning { get; set; } = true;
    public bool SendChannelWarning { get; set; } = false;

    [MaxLength(500)]
    public string? DirectMessageTemplate { get; set; }

    [MaxLength(300)]
    public string? ChannelWarningTemplate { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class LinkFilterRule
{
    [Key]
    public int Id { get; set; }

    public ulong GuildId { get; set; }

    [MaxLength(255)]
    public string NormalizedDomain { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class LinkFilterBypassRole
{
    [Key]
    public int Id { get; set; }

    public ulong GuildId { get; set; }
    public ulong RoleId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class LinkFilterBypassChannel
{
    [Key]
    public int Id { get; set; }

    public ulong GuildId { get; set; }
    public ulong ChannelId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
