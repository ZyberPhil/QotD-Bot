using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using QotD.Bot.Data;
using QotD.Bot.Features.Logging.Models;

namespace QotD.Bot.Features.Logging.Services;

public sealed class DiscordLoggingEventHandler :
    IEventHandler<MessageDeletedEventArgs>,
    IEventHandler<MessageUpdatedEventArgs>,
    IEventHandler<GuildMemberAddedEventArgs>,
    IEventHandler<GuildMemberRemovedEventArgs>,
    IEventHandler<VoiceStateUpdatedEventArgs>
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DiscordLoggingEventHandler> _logger;

    public DiscordLoggingEventHandler(IServiceProvider serviceProvider, ILogger<DiscordLoggingEventHandler> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    private async Task SendLogAsync(DiscordClient client, ulong guildId, LogType type, DiscordEmbed embed)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var config = await db.LogRoutingConfigs
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.GuildId == guildId && c.LogType == type && c.IsEnabled);

            if (config != null && config.ChannelId > 0)
            {
                if (client.Guilds.TryGetValue(guildId, out var guild))
                {
                    if (guild.Channels.TryGetValue(config.ChannelId, out var channel))
                    {
                        var builder = new DiscordMessageBuilder().AddEmbed(embed);
                        await channel.SendMessageAsync(builder);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send {LogType} log to channel", type);
        }
    }

    public async Task HandleEventAsync(DiscordClient client, MessageDeletedEventArgs e)
    {
        if (e.Guild == null || e.Message == null || e.Message.Author?.IsBot == true) return;

        var embed = new DiscordEmbedBuilder()
            .WithTitle("🗑️ Message Deleted")
            .WithColor(DiscordColor.Red)
            .WithDescription($"A message by <@{e.Message.Author?.Id}> was deleted in <#{e.Channel.Id}>.\n\n**Content:**\n{e.Message.Content}")
            .WithTimestamp(DateTimeOffset.UtcNow)
            .WithFooter($"Author ID: {e.Message.Author?.Id} | Message ID: {e.Message.Id}");

        await SendLogAsync(client, e.Guild.Id, LogType.MessageDeleted, embed.Build());
    }

    public async Task HandleEventAsync(DiscordClient client, MessageUpdatedEventArgs e)
    {
        if (e.Guild == null || e.MessageBefore == null || e.Message?.Author?.IsBot == true) return;
        if (e.MessageBefore.Content == e.Message?.Content) return; // Only log content changes

        var embed = new DiscordEmbedBuilder()
            .WithTitle("✏️ Message Edited")
            .WithColor(DiscordColor.Orange)
            .WithDescription($"[Jump to message]({e.Message?.JumpLink}) in <#{e.Channel.Id}>\n\n**Before:**\n{e.MessageBefore.Content}\n\n**After:**\n{e.Message?.Content}")
            .WithTimestamp(DateTimeOffset.UtcNow)
            .WithFooter($"Author ID: {e.Message?.Author?.Id} | Message ID: {e.Message?.Id}");

        await SendLogAsync(client, e.Guild.Id, LogType.MessageUpdated, embed.Build());
    }

    public async Task HandleEventAsync(DiscordClient client, GuildMemberAddedEventArgs e)
    {
        var embed = new DiscordEmbedBuilder()
            .WithTitle("👋 Member Joined")
            .WithColor(DiscordColor.Green)
            .WithDescription($"<@{e.Member.Id}> joined the server.")
            .WithThumbnail(e.Member.AvatarUrl)
            .WithTimestamp(DateTimeOffset.UtcNow)
            .WithFooter($"ID: {e.Member.Id}");

        await SendLogAsync(client, e.Guild.Id, LogType.MemberJoined, embed.Build());
    }

    public async Task HandleEventAsync(DiscordClient client, GuildMemberRemovedEventArgs e)
    {
        var embed = new DiscordEmbedBuilder()
            .WithTitle("🚪 Member Left")
            .WithColor(DiscordColor.Red)
            .WithDescription($"<@{e.Member.Id}> left the server.")
            .WithThumbnail(e.Member.AvatarUrl)
            .WithTimestamp(DateTimeOffset.UtcNow)
            .WithFooter($"ID: {e.Member.Id}");

        await SendLogAsync(client, e.Guild.Id, LogType.MemberLeft, embed.Build());
    }

    public async Task HandleEventAsync(DiscordClient client, VoiceStateUpdatedEventArgs e)
    {
        var beforeChannelId = e.Before?.ChannelId ?? 0;
        var afterChannelId = e.After?.ChannelId ?? 0;

        if (beforeChannelId == afterChannelId) return; // No channel change

        var userId = e.Before?.UserId ?? e.After?.UserId ?? 0;
        var guildId = e.Before?.GuildId ?? e.After?.GuildId ?? 0;

        if (userId == 0 || guildId == 0) return;

        var embed = new DiscordEmbedBuilder()
            .WithColor(DiscordColor.Azure)
            .WithTimestamp(DateTimeOffset.UtcNow)
            .WithFooter($"ID: {userId}");

        if (beforeChannelId == 0 && afterChannelId != 0)
        {
            embed.WithTitle("🎤 Joined Voice Channel");
            embed.WithDescription($"<@{userId}> joined voice channel <#{afterChannelId}>.");
        }
        else if (beforeChannelId != 0 && afterChannelId == 0)
        {
            embed.WithTitle("🎤 Left Voice Channel");
            embed.WithDescription($"<@{userId}> left voice channel <#{beforeChannelId}>.");
        }
        else if (beforeChannelId != 0 && afterChannelId != 0)
        {
            embed.WithTitle("🎤 Switched Voice Channel");
            embed.WithDescription($"<@{userId}> moved from <#{beforeChannelId}> to <#{afterChannelId}>.");
        }

        await SendLogAsync(client, guildId, LogType.VoiceStateUpdated, embed.Build());
    }
}
