using System.Collections.Concurrent;

namespace QotD.Bot.Services;

/// <summary>
/// Simple service to track users who are in the process of setting a message template.
/// </summary>
public sealed class TemplateSessionService
{
    // Key: (UserId, GuildId)
    private readonly ConcurrentDictionary<(ulong UserId, ulong GuildId), DateTime> _sessions = new();

    public void StartSession(ulong userId, ulong guildId)
    {
        _sessions[(userId, guildId)] = DateTime.UtcNow;
    }

    public bool IsInSession(ulong userId, ulong guildId)
    {
        if (_sessions.TryGetValue((userId, guildId), out var startTime))
        {
            // Session expires after 5 minutes
            if (DateTime.UtcNow - startTime < TimeSpan.FromMinutes(5))
            {
                return true;
            }
            
            _sessions.TryRemove((userId, guildId), out _);
        }
        return false;
    }

    public void EndSession(ulong userId, ulong guildId)
    {
        _sessions.TryRemove((userId, guildId), out _);
    }
}
