using System.ComponentModel.DataAnnotations;

namespace QotD.Bot.Data.Models;

public sealed class Ticket
{
    public int Id { get; set; }

    public ulong GuildId { get; set; }

    public int TicketNumber { get; set; }

    public ulong ChannelId { get; set; }

    public ulong CreatedByUserId { get; set; }

    public ulong? ClaimedByUserId { get; set; }

    public TicketType Type { get; set; } = TicketType.Support;

    public TicketPriority Priority { get; set; } = TicketPriority.Medium;

    public TicketStatus Status { get; set; } = TicketStatus.Open;

    [MaxLength(200)]
    public string? Subject { get; set; }

    [MaxLength(500)]
    public string? CloseReason { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? ClaimedAtUtc { get; set; }

    public DateTimeOffset? ClosedAtUtc { get; set; }

    public DateTimeOffset? LastActivityAtUtc { get; set; }
}

public sealed class TicketStaffRole
{
    public int Id { get; set; }

    public ulong GuildId { get; set; }

    public ulong RoleId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public TicketConfig? Config { get; set; }
}

public sealed class TicketLogConfig
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public ulong GuildId { get; set; }

    public TicketLogEventType EventType { get; set; }

    public ulong ChannelId { get; set; }

    public bool IsEnabled { get; set; } = true;

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class TicketTranscript
{
    public int Id { get; set; }

    public ulong GuildId { get; set; }

    public int TicketId { get; set; }

    public ulong TicketChannelId { get; set; }

    [MaxLength(200)]
    public string FileName { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public ulong GeneratedByUserId { get; set; }

    public DateTimeOffset GeneratedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
