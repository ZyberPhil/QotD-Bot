using System.ComponentModel.DataAnnotations;

namespace QotD.Bot.Data.Models;

public sealed class CountingChannelConfig
{
    [Key]
    public int Id { get; set; }
    
    public ulong GuildId { get; set; }
    public ulong ChannelId { get; set; }
    
    public int CurrentCount { get; set; } = 0;
    public ulong LastUserId { get; set; } = 0;
    public int Highscore { get; set; } = 0;
    
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
