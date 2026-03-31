using System.ComponentModel.DataAnnotations;

namespace QotD.Bot.Data.Models;

public sealed class TempVoiceConfig
{
    [Key]
    public ulong GuildId { get; set; }
    public ulong TriggerChannelId { get; set; }
    public ulong? CategoryId { get; set; }
}
