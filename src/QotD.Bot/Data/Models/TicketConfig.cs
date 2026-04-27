using System.ComponentModel.DataAnnotations;

namespace QotD.Bot.Data.Models;

public sealed class TicketConfig
{
    [Key]
    public ulong GuildId { get; set; }

    public bool IsEnabled { get; set; } = true;

    public ulong CategoryId { get; set; }

    [MaxLength(500)]
    public string? PanelDescription { get; set; }

    public int MaxOpenTicketsPerUser { get; set; } = 2;

    public int DefaultSlaMinutes { get; set; } = 180;

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<TicketStaffRole> StaffRoles { get; set; } = [];
}
