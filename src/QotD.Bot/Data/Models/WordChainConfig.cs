using System.ComponentModel.DataAnnotations;

namespace QotD.Bot.Data.Models;

public sealed class WordChainConfig
{
    [Key]
    public int Id { get; set; }
    
    public ulong GuildId { get; set; }
    public ulong ChannelId { get; set; }
    
    public string? LastWord { get; set; }
    public ulong LastUserId { get; set; } = 0;
    public string UsedWordsJson { get; set; } = "[]";
    public int ChainLength { get; set; } = 0;
    public int Highscore { get; set; } = 0;
    
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
