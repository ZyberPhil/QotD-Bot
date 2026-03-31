namespace QotD.Bot.Features.Logging.Models;

public enum LogType
{
    // Server Events
    MessageDeleted,
    MessageUpdated,
    MemberJoined,
    MemberLeft,
    VoiceStateUpdated,
    
    // Bot internals
    BotAction,
    BotError
}
