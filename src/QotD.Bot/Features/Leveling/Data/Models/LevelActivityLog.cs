namespace QotD.Bot.Features.Leveling.Data.Models;

public enum LevelActivityType
{
    Message = 0,
    VoiceMinute = 1
}

public sealed class LevelActivityLog
{
    public long Id { get; set; }

    public long UserId { get; set; }

    public long GuildId { get; set; }

    public LevelActivityType ActivityType { get; set; }

    public int Amount { get; set; }

    public DateTimeOffset OccurredAtUtc { get; set; }
}