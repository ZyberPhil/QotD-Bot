using Serilog.Core;
using Serilog.Events;

namespace QotD.Bot.Features.Logging.Services;

public sealed class DiscordBotSerilogSink(DiscordBotLogRelay relay) : ILogEventSink
{
    public void Emit(LogEvent logEvent)
    {
        if (logEvent is null)
        {
            return;
        }

        if (!TryGetSource(logEvent, out var source))
        {
            source = "Unknown";
        }

        // Prevent recursive forwarding for relay/pump internals.
        if (source.StartsWith("QotD.Bot.Features.Logging.Services.DiscordBot", StringComparison.Ordinal))
        {
            return;
        }

        var rendered = logEvent.RenderMessage();
        if (string.IsNullOrWhiteSpace(rendered))
        {
            return;
        }

        if (logEvent.Exception is not null)
        {
            rendered = $"{rendered}\n{logEvent.Exception.GetType().Name}: {logEvent.Exception.Message}";
        }

        relay.Enqueue(logEvent.Level, source, rendered);
    }

    private static bool TryGetSource(LogEvent logEvent, out string source)
    {
        source = string.Empty;
        if (!logEvent.Properties.TryGetValue("SourceContext", out var sourceProperty))
        {
            return false;
        }

        var text = sourceProperty.ToString().Trim('"');
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        source = text;
        return true;
    }
}
