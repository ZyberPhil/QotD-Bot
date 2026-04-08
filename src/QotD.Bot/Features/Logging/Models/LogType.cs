namespace QotD.Bot.Features.Logging.Models;

public enum LogType
{
    // Server Events
    MessageDeleted,
    MessageUpdated,
    MemberJoined,
    MemberLeft,
    VoiceStateUpdated,

    // Combined routing categories
    MemberJoinLeave,
    VoiceJoinLeave,
    
    // Bot internals
    BotAction,
    BotError
}
