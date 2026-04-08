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

    private async Task SendLogAsync(DiscordClient client, ulong guildId, DiscordEmbed embed, params LogType[] types)
    {
        try
        {
            if (types.Length == 0)
            {
                return;
            }

            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var routingTypes = types.Distinct().ToList();
            var configs = await db.LogRoutingConfigs
                .AsNoTracking()
                .Where(c => c.GuildId == guildId && c.IsEnabled && routingTypes.Contains(c.LogType) && c.ChannelId > 0)
                .ToListAsync();

            if (configs.Count == 0)
            {
                return;
            }

            if (client.Guilds.TryGetValue(guildId, out var guild))
            {
                foreach (var channelId in configs.Select(c => c.ChannelId).Distinct())
                {
                    if (guild.Channels.TryGetValue(channelId, out var channel))
                    {
                        var builder = new DiscordMessageBuilder().AddEmbed(embed);
                        await channel.SendMessageAsync(builder);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            var route = string.Join(", ", types.Select(t => t.ToString()));
            _logger.LogError(ex, "Failed to send log to channel. Route types: {RouteTypes}", route);
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

        await SendLogAsync(client, e.Guild.Id, embed.Build(), LogType.MessageDeleted);
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

        await SendLogAsync(client, e.Guild.Id, embed.Build(), LogType.MessageUpdated);
    }

    public async Task HandleEventAsync(DiscordClient client, GuildMemberAddedEventArgs e)
    {
        var roles = FormatRoles(e.Member.Roles, e.Guild.EveryoneRole.Id);
        var joinedAt = FormatDate(e.Member.JoinedAt);

        var embed = new DiscordEmbedBuilder()
            .WithTitle("👋 Member Joined")
            .WithColor(DiscordColor.Green)
            .WithDescription($"{e.Member.Mention} ist dem Server beigetreten.")
            .AddField("Benutzer", $"{e.Member.Mention}\n{e.Member.Username}", true)
            .AddField("Beitrittsdatum", joinedAt, true)
            .AddField("Rollen", roles)
            .WithTimestamp(DateTimeOffset.UtcNow)
            .WithFooter($"User ID: {e.Member.Id} | Join Event");

        if (!string.IsNullOrWhiteSpace(e.Member.AvatarUrl))
        {
            embed.WithThumbnail(e.Member.AvatarUrl);
        }

        await SendLogAsync(client, e.Guild.Id, embed.Build(), LogType.MemberJoinLeave, LogType.MemberJoined);
    }

    public async Task HandleEventAsync(DiscordClient client, GuildMemberRemovedEventArgs e)
    {
        var roles = FormatRoles(e.Member.Roles, e.Guild.EveryoneRole.Id);
        var joinedAt = FormatDate(e.Member.JoinedAt);

        var embed = new DiscordEmbedBuilder()
            .WithTitle("🚪 Member Left")
            .WithColor(DiscordColor.Red)
            .WithDescription($"{e.Member.Mention} hat den Server verlassen.")
            .AddField("Benutzer", $"{e.Member.Mention}\n{e.Member.Username}", true)
            .AddField("Beitrittsdatum", joinedAt, true)
            .AddField("Rollen", roles)
            .WithTimestamp(DateTimeOffset.UtcNow)
            .WithFooter($"User ID: {e.Member.Id} | Leave Event");

        if (!string.IsNullOrWhiteSpace(e.Member.AvatarUrl))
        {
            embed.WithThumbnail(e.Member.AvatarUrl);
        }

        await SendLogAsync(client, e.Guild.Id, embed.Build(), LogType.MemberJoinLeave, LogType.MemberLeft);
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

        await SendLogAsync(client, guildId, embed.Build(), LogType.VoiceJoinLeave, LogType.VoiceStateUpdated);
    }

    private static string FormatRoles(IEnumerable<DiscordRole> roles, ulong everyoneRoleId)
    {
        var roleMentions = roles
            .Where(r => r.Id != everyoneRoleId)
            .OrderByDescending(r => r.Position)
            .Select(r => r.Mention)
            .ToList();

        return roleMentions.Count > 0
            ? string.Join(", ", roleMentions)
            : "Keine Rollen";
    }

    private static string FormatDate(DateTimeOffset? value)
    {
        if (value is null)
        {
            return "Unbekannt";
        }

        return $"<t:{value.Value.ToUnixTimeSeconds()}:F>";
    }
}
