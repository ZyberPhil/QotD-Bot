using System.ComponentModel.DataAnnotations;

namespace QotD.Bot.Data.Models;

public enum AutoModerationAuditAction
{
    MessageBlocked = 0,
    LockdownActivated = 1,
    LockdownEnded = 2,
    JoinObserved = 3,
    ConfigChanged = 4
}

public sealed class AutoModerationConfig
{
    [Key]
    public ulong GuildId { get; set; }

    public bool IsEnabled { get; set; } = false;

    public bool RaidModeEnabled { get; set; } = true;
    public int RaidJoinThreshold { get; set; } = 8;
    public int RaidWindowSeconds { get; set; } = 30;
    public int RaidLockdownMinutes { get; set; } = 30;

    public bool IsLockdownActive { get; set; } = false;
    public DateTimeOffset? LockdownActivatedAtUtc { get; set; }
    public DateTimeOffset? LockdownEndsAtUtc { get; set; }

    public bool RestrictToVerifiedRoleDuringLockdown { get; set; } = true;
    public ulong? VerifiedRoleId { get; set; }
    public int LockdownMinAccountAgeHours { get; set; } = 24;

    public bool EnforceAccountAgeForLinks { get; set; } = true;
    public int MinAccountAgeDaysForLinks { get; set; } = 7;

    public bool EnforceServerAgeForLinks { get; set; } = true;
    public int MinServerAgeHoursForLinks { get; set; } = 24;

    public ulong? LogChannelId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class AutoModerationRaidIncident
{
    [Key]
    public int Id { get; set; }

    public ulong GuildId { get; set; }

    public DateTimeOffset StartedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? EndedAtUtc { get; set; }

    public int TriggerJoinCount { get; set; }
    public int WindowSeconds { get; set; }

    [MaxLength(400)]
    public string? Notes { get; set; }
}

public sealed class AutoModerationAuditEntry
{
    [Key]
    public long Id { get; set; }

    public ulong GuildId { get; set; }
    public ulong? UserId { get; set; }
    public ulong? ChannelId { get; set; }
    public ulong? MessageId { get; set; }

    public AutoModerationAuditAction Action { get; set; }

    [MaxLength(120)]
    public string RuleKey { get; set; } = string.Empty;

    [MaxLength(500)]
    public string Reason { get; set; } = string.Empty;

    [MaxLength(1800)]
    public string? Evidence { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
