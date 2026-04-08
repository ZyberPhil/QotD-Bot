using DSharpPlus;
using DSharpPlus.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using QotD.Bot.Data;
using QotD.Bot.Features.Logging.Models;
using Serilog.Events;

namespace QotD.Bot.Features.Logging.Services;

public sealed class DiscordBotLogPump(
    IServiceScopeFactory scopeFactory,
    IServiceProvider serviceProvider,
    DiscordBotLogRelay relay) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var entry in relay.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await DispatchToConfiguredChannelsAsync(entry, stoppingToken);
            }
            catch
            {
                // Never throw from the pump; otherwise log forwarding would stop entirely.
            }
        }
    }

    private async Task DispatchToConfiguredChannelsAsync(DiscordBotLogRelay.BotLogEntry entry, CancellationToken ct)
    {
        var logType = entry.Level >= LogEventLevel.Warning
            ? LogType.BotError
            : LogType.BotAction;

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var configs = await db.LogRoutingConfigs
            .AsNoTracking()
            .Where(c => c.LogType == logType && c.IsEnabled && c.ChannelId > 0)
            .ToListAsync(ct);

        if (configs.Count == 0)
        {
            return;
        }

        var client = serviceProvider.GetRequiredService<DiscordClient>();
        var embed = BuildEmbed(entry, logType);

        foreach (var config in configs)
        {
            if (ct.IsCancellationRequested)
            {
                break;
            }

            if (!client.Guilds.TryGetValue(config.GuildId, out var guild))
            {
                continue;
            }

            if (!guild.Channels.TryGetValue(config.ChannelId, out var channel))
            {
                continue;
            }

            await channel.SendMessageAsync(new DiscordMessageBuilder().AddEmbed(embed));
        }
    }

    private static DiscordEmbed BuildEmbed(DiscordBotLogRelay.BotLogEntry entry, LogType type)
    {
        var color = type == LogType.BotError ? DiscordColor.Red : DiscordColor.Blurple;
        var title = type == LogType.BotError ? "Bot Error" : "Bot Action";

        var message = entry.Message;
        if (message.Length > 1800)
        {
            message = message[..1800] + "...";
        }

        return new DiscordEmbedBuilder()
            .WithTitle(title)
            .WithColor(color)
            .WithDescription(message)
            .AddField("Source", entry.Source)
            .WithTimestamp(entry.Timestamp)
            .Build();
    }
}
