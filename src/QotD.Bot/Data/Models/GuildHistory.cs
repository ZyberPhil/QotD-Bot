using System.ComponentModel.DataAnnotations;

namespace QotD.Bot.Data.Models;

/// <summary>
/// History of questions posted to specific guilds.
/// </summary>
public sealed class GuildHistory
{
    public int Id { get; set; }

    public ulong GuildId { get; set; }

    public int QuestionId { get; set; }

    public DateTimeOffset PostedAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation properties
    public Question Question { get; set; } = null!;
}
