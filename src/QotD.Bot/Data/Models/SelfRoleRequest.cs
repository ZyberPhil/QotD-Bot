namespace QotD.Bot.Data.Models;

public enum SelfRoleRequestStatus
{
    Pending = 0,
    Approved = 1,
    Denied = 2,
    Cancelled = 3
}

public sealed class SelfRoleRequest
{
    public long Id { get; set; }

    public ulong GuildId { get; set; }
    public ulong UserId { get; set; }
    public ulong RoleId { get; set; }
    public ulong? PanelMessageId { get; set; }
    public ulong? ModerationChannelId { get; set; }
    public ulong? ModerationMessageId { get; set; }
    public ulong? ModeratorId { get; set; }

    public SelfRoleRequestStatus Status { get; set; } = SelfRoleRequestStatus.Pending;
    public string? Reason { get; set; }

    public DateTimeOffset RequestedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ResolvedAtUtc { get; set; }
}