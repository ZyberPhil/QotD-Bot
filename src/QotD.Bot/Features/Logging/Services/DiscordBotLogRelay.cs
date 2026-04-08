using System.Threading.Channels;
using Serilog.Events;

namespace QotD.Bot.Features.Logging.Services;

public sealed class DiscordBotLogRelay
{
    private readonly Channel<BotLogEntry> _queue = Channel.CreateUnbounded<BotLogEntry>(new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = false
    });

    public ChannelReader<BotLogEntry> Reader => _queue.Reader;

    public void Enqueue(LogEventLevel level, string source, string message)
    {
        _queue.Writer.TryWrite(new BotLogEntry(level, source, message, DateTimeOffset.UtcNow));
    }

    public sealed record BotLogEntry(LogEventLevel Level, string Source, string Message, DateTimeOffset Timestamp);
}
