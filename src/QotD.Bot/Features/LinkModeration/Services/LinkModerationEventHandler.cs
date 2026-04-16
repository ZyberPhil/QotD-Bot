using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using QotD.Bot.Data.Models;
using QotD.Bot.UI;

namespace QotD.Bot.Features.LinkModeration.Services;

public sealed class LinkModerationEventHandler : IEventHandler<MessageCreatedEventArgs>
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LinkModerationEventHandler> _logger;

    public LinkModerationEventHandler(IServiceScopeFactory scopeFactory, ILogger<LinkModerationEventHandler> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task HandleEventAsync(DiscordClient client, MessageCreatedEventArgs e)
    {
        if (e.Guild is null)
        {
            return;
        }

        if (e.Author is null || e.Author.IsBot)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(e.Message.Content))
        {
            return;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<LinkModerationService>();

            var member = await ResolveMemberAsync(e);
            if (member is null)
            {
                return;
            }

            if (HasBypassPermissions(member))
            {
                return;
            }

            var roleIds = member.Roles.Select(x => x.Id).ToArray();
            var decision = await service.EvaluateAsync(e.Guild.Id, e.Channel.Id, roleIds, e.Message.Content);
            if (!decision.IsEnabled || !decision.ShouldBlock || decision.Config is null)
            {
                return;
            }

            await e.Message.DeleteAsync();

            await SendDirectMessageWarningAsync(member, decision.Config, decision.BlockedLinks);
            await SendChannelWarningAsync(e.Channel, member, decision.Config);
            await SendLogAsync(client, e.Guild, e.Channel, member, decision.Config, decision.BlockedLinks, e.Message.Content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Link moderation failed for guild {GuildId}", e.Guild.Id);
        }
    }

    private static bool HasBypassPermissions(DiscordMember member)
    {
        var permissions = member.Permissions;
        return permissions.HasPermission(DiscordPermission.Administrator)
            || permissions.HasPermission(DiscordPermission.ManageMessages);
    }

    private static async Task<DiscordMember?> ResolveMemberAsync(MessageCreatedEventArgs e)
    {
        if (e.Author is DiscordMember directMember)
        {
            return directMember;
        }

        if (e.Guild is null)
        {
            return null;
        }

        if (e.Guild.Members.TryGetValue(e.Author.Id, out var cachedMember))
        {
            return cachedMember;
        }

        try
        {
            return await e.Guild.GetMemberAsync(e.Author.Id);
        }
        catch
        {
            return null;
        }
    }

    private async Task SendDirectMessageWarningAsync(DiscordMember member, LinkFilterConfig config, IReadOnlyList<string> blockedLinks)
    {
        if (!config.SendDirectMessageWarning)
        {
            return;
        }

        try
        {
            var links = blockedLinks.Count > 0
                ? string.Join("\n", blockedLinks.Select(x => $"- {x}"))
                : "- (unknown)";

            var template = config.DirectMessageTemplate;
            var content = string.IsNullOrWhiteSpace(template)
                ? "Deine Nachricht wurde gelöscht, weil sie einen nicht erlaubten Link enthält."
                : template;

            content = content.Replace("{BlockedLinks}", links, StringComparison.OrdinalIgnoreCase);

            await member.SendMessageAsync(content);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send link warning DM to user {UserId}", member.Id);
        }
    }

    private async Task SendChannelWarningAsync(DiscordChannel channel, DiscordMember member, LinkFilterConfig config)
    {
        if (!config.SendChannelWarning)
        {
            return;
        }

        try
        {
            var template = config.ChannelWarningTemplate;
            var content = string.IsNullOrWhiteSpace(template)
                ? "{UserMention} dein Link ist in diesem Kanal nicht erlaubt."
                : template;

            content = content.Replace("{UserMention}", member.Mention, StringComparison.OrdinalIgnoreCase);
            await channel.SendMessageAsync(content);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send channel warning in channel {ChannelId}", channel.Id);
        }
    }

    private async Task SendLogAsync(
        DiscordClient client,
        DiscordGuild guild,
        DiscordChannel sourceChannel,
        DiscordMember member,
        LinkFilterConfig config,
        IReadOnlyList<string> blockedLinks,
        string messageContent)
    {
        if (config.LogChannelId is null || config.LogChannelId.Value == 0)
        {
            return;
        }

        if (!guild.Channels.TryGetValue(config.LogChannelId.Value, out var logChannel))
        {
            return;
        }

        var links = blockedLinks.Count > 0
            ? string.Join("\n", blockedLinks.Select(x => $"- {x}"))
            : "- (unknown)";

        var mode = config.Mode == LinkFilterMode.Whitelist ? "Whitelist" : "Blacklist";

        var embed = SectorUI.CreateLogEmbed(
                $"{BotEmojis.Warning} Link Blocked",
                $"Nachricht von {member.Mention} wurde in {sourceChannel.Mention} entfernt.",
                SectorUI.SectorWarning,
                $"User ID: {member.Id} | Channel ID: {sourceChannel.Id}")
            .AddField("Mode", mode, true)
            .AddField("Blocked Links", links)
            .AddField("Message", string.IsNullOrWhiteSpace(messageContent) ? "(empty)" : messageContent)
            .WithTimestamp(DateTimeOffset.UtcNow);

        await logChannel.SendMessageAsync(new DiscordMessageBuilder().AddEmbed(embed));
    }
}
