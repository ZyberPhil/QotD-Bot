using System.ComponentModel.DataAnnotations;

namespace QotD.Bot.Data.Models;

public enum TeamWarningType
{
    Manual = 0,
    MissingMinimum = 1
}

public sealed class TeamWarning
{
    public int Id { get; set; }

    [Required]
    public ulong GuildId { get; set; }

    [Required]
    public ulong UserId { get; set; }

    public ulong? RoleId { get; set; }

    public TeamWarningType WarningType { get; set; } = TeamWarningType.Manual;

    [Required]
    [MaxLength(500)]
    public string Reason { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public ulong CreatedByUserId { get; set; }

    public bool IsActive { get; set; } = true;

    public bool IsResolved { get; set; } = false;

    public DateTimeOffset? ResolvedAtUtc { get; set; }

    public ulong? ResolvedByUserId { get; set; }

    [MaxLength(1000)]
    public string? ResolutionNote { get; set; }

    public DateTimeOffset? WeekStartUtc { get; set; }

    public ICollection<TeamWarningNote> Notes { get; set; } = [];
}