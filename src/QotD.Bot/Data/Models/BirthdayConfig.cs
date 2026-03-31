using System.ComponentModel.DataAnnotations;

namespace QotD.Bot.Data.Models;

public sealed class BirthdayConfig
{
    [Key]
    public ulong GuildId { get; set; }
    public ulong AnnouncementChannelId { get; set; }
    public ulong BirthdayRoleId { get; set; }
    public DateOnly? LastAnnouncementDate { get; set; }
}
