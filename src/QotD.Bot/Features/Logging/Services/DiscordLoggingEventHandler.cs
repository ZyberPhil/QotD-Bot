using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using QotD.Bot.Data;
using QotD.Bot.Features.Logging.Models;
using QotD.Bot.UI;

namespace QotD.Bot.Features.Logging.Services;

public sealed class DiscordLoggingEventHandler :
    IEventHandler<MessageDeletedEventArgs>,
    IEventHandler<MessageUpdatedEventArgs>,
    IEventHandler<GuildMemberAddedEventArgs>,
    IEventHandler<GuildMemberRemovedEventArgs>,
    IEventHandler<VoiceStateUpdatedEventArgs>
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DiscordLoggingEventHandler> _logger;

    public DiscordLoggingEventHandler(IServiceScopeFactory scopeFactory, ILogger<DiscordLoggingEventHandler> logger)
    {
        _scopeFactory = scopeFactory;
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

            using var scope = _scopeFactory.CreateScope();
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

        var content = string.IsNullOrWhiteSpace(e.Message.Content) ? "*No text content*" : e.Message.Content;
        var embed = SectorUI.CreateLogEmbed(
            $"{BotEmojis.Delete} Message Deleted",
            $"A message by <@{e.Message.Author?.Id}> was deleted in <#{e.Channel.Id}>.\n\n**Content:**\n{content}",
            SectorUI.SectorDanger,
            $"Author ID: {e.Message.Author?.Id} | Message ID: {e.Message.Id}");

        await SendLogAsync(client, e.Guild.Id, embed.Build(), LogType.MessageDeleted);
    }

    public async Task HandleEventAsync(DiscordClient client, MessageUpdatedEventArgs e)
    {
        if (e.Guild == null || e.MessageBefore == null || e.Message?.Author?.IsBot == true) return;
        if (e.MessageBefore.Content == e.Message?.Content) return; // Only log content changes

        var before = string.IsNullOrWhiteSpace(e.MessageBefore.Content) ? "*No text content*" : e.MessageBefore.Content;
        var after = string.IsNullOrWhiteSpace(e.Message?.Content) ? "*No text content*" : e.Message.Content;
        var embed = SectorUI.CreateLogEmbed(
            $"{BotEmojis.Edit} Message Edited",
            $"[Jump to message]({e.Message?.JumpLink}) in <#{e.Channel.Id}>\n\n**Before:**\n{before}\n\n**After:**\n{after}",
            SectorUI.SectorWarning,
            $"Author ID: {e.Message?.Author?.Id} | Message ID: {e.Message?.Id}");

        await SendLogAsync(client, e.Guild.Id, embed.Build(), LogType.MessageUpdated);
    }

    public async Task HandleEventAsync(DiscordClient client, GuildMemberAddedEventArgs e)
    {
        var roles = FormatRoles(e.Member.Roles, e.Guild.EveryoneRole.Id);
        var joinedAt = FormatDate(e.Member.JoinedAt);

        var embed = SectorUI.CreateLogEmbed(
                $"{BotEmojis.Join} Member Joined",
                $"{e.Member.Mention} ist dem Server beigetreten.",
                SectorUI.SectorSuccessGreen,
                $"User ID: {e.Member.Id} | Join Event")
            .AddField("Benutzer", $"{e.Member.Mention}\n{e.Member.Username}", true)
            .AddField("Beitrittsdatum", joinedAt, true)
            .AddField("Rollen", roles);

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

        var embed = SectorUI.CreateLogEmbed(
                $"{BotEmojis.Leave} Member Left",
                $"{e.Member.Mention} hat den Server verlassen.",
                SectorUI.SectorDanger,
                $"User ID: {e.Member.Id} | Leave Event")
            .AddField("Benutzer", $"{e.Member.Mention}\n{e.Member.Username}", true)
            .AddField("Beitrittsdatum", joinedAt, true)
            .AddField("Rollen", roles);

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

        var embed = SectorUI.CreateLogEmbed(
            $"{BotEmojis.Voice} Voice Update",
            "",
            SectorUI.SectorInfoBlue,
            $"User ID: {userId}");

        if (beforeChannelId == 0 && afterChannelId != 0)
        {
            embed.WithTitle($"{BotEmojis.Voice} Joined Voice Channel");
            embed.WithDescription($"<@{userId}> joined voice channel <#{afterChannelId}>.");
        }
        else if (beforeChannelId != 0 && afterChannelId == 0)
        {
            embed.WithTitle($"{BotEmojis.Voice} Left Voice Channel");
            embed.WithDescription($"<@{userId}> left voice channel <#{beforeChannelId}>.");
        }
        else if (beforeChannelId != 0 && afterChannelId != 0)
        {
            embed.WithTitle($"{BotEmojis.Voice} Switched Voice Channel");
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
