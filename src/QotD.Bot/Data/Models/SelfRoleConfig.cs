using System.ComponentModel.DataAnnotations;

namespace QotD.Bot.Data.Models;

public sealed class SelfRoleConfig
{
    [Key]
    public ulong GuildId { get; set; }

    public ulong? PanelChannelId { get; set; }
    public ulong? PanelMessageId { get; set; }
    public ulong? ModerationChannelId { get; set; }

    public bool IsEnabled { get; set; } = true;
    public bool AllowMultipleRoles { get; set; } = true;
    public bool RequireModeration { get; set; } = false;

    public string? PanelTitle { get; set; }
    public string? PanelDescriptionTemplate { get; set; }
    public string? PanelFooter { get; set; }
    public string? PanelColorHex { get; set; }
    public string? PanelThumbnailUrl { get; set; }
    public string? PanelImageUrl { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}