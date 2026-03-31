using System.ComponentModel.DataAnnotations;
using QotD.Bot.Features.Logging.Models;

namespace QotD.Bot.Data.Models;

public sealed class LogRoutingConfig
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public ulong GuildId { get; set; }

    public LogType LogType { get; set; }

    public ulong ChannelId { get; set; }

    public bool IsEnabled { get; set; } = true;
    
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
