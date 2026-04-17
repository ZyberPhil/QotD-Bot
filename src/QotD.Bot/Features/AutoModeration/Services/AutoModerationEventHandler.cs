using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using QotD.Bot.Data.Models;
using QotD.Bot.UI;

namespace QotD.Bot.Features.AutoModeration.Services;

public sealed class AutoModerationEventHandler :
    IEventHandler<GuildMemberAddedEventArgs>,
    IEventHandler<MessageCreatedEventArgs>
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AutoModerationEventHandler> _logger;

    public AutoModerationEventHandler(IServiceScopeFactory scopeFactory, ILogger<AutoModerationEventHandler> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task HandleEventAsync(DiscordClient client, GuildMemberAddedEventArgs e)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<AutoModerationService>();

            var raidDecision = await service.RegisterJoinAndEvaluateRaidAsync(e.Guild.Id);
            if (!raidDecision.TriggeredLockdown || raidDecision.Config is null)
            {
                return;
            }

            await SendLockdownLogAsync(client, e.Guild, raidDecision.Config, raidDecision.JoinCountInWindow);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Auto moderation join handling failed for guild {GuildId}", e.Guild.Id);
        }
    }

    public async Task HandleEventAsync(DiscordClient client, MessageCreatedEventArgs e)
    {
        if (e.Guild is null || e.Author is null || e.Author.IsBot)
        {
            return;
        }

        var member = await ResolveMemberAsync(e);
        if (member is null)
        {
            return;
        }

        if (HasBypassPermissions(member))
        {
            return;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<AutoModerationService>();

            var decision = await service.EvaluateMessageAsync(member, e.Message);
            if (!decision.ShouldBlock || decision.Config is null)
            {
                return;
            }

            await e.Message.DeleteAsync();

            await service.AddAuditEntryAsync(new AutoModerationAuditEntry
            {
                GuildId = e.Guild.Id,
                UserId = member.Id,
                ChannelId = e.Channel.Id,
                MessageId = e.Message.Id,
                Action = AutoModerationAuditAction.MessageBlocked,
                RuleKey = decision.RuleKey,
                Reason = decision.Reason,
                Evidence = decision.Evidence
            });

            await SendUserWarningAsync(member, decision.Reason);
            await SendChannelLogAsync(e.Guild, decision.Config, member, e.Channel, decision.Reason, decision.RuleKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Auto moderation message handling failed for guild {GuildId}", e.Guild.Id);
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

    private static async Task SendUserWarningAsync(DiscordMember member, string reason)
    {
        try
        {
            await member.SendMessageAsync($"Deine Nachricht wurde von AutoMod blockiert: {reason}");
        }
        catch
        {
            // Ignore DM failures.
        }
    }

    private static async Task SendLockdownLogAsync(DiscordClient client, DiscordGuild guild, AutoModerationConfig config, int joinCount)
    {
        if (config.LogChannelId is null || config.LogChannelId.Value == 0)
        {
            return;
        }

        if (!guild.Channels.TryGetValue(config.LogChannelId.Value, out var channel))
        {
            return;
        }

        var embed = SectorUI.CreateLogEmbed(
                $"{BotEmojis.Warning} Raid Lockdown Activated",
                $"Automatic lockdown activated after {joinCount} joins in {config.RaidWindowSeconds}s.",
                SectorUI.SectorWarning,
                $"Guild ID: {guild.Id}")
            .AddField("Lockdown Duration", $"{config.RaidLockdownMinutes} minute(s)", true)
            .AddField("Ends", config.LockdownEndsAtUtc is null ? "Unknown" : $"<t:{config.LockdownEndsAtUtc.Value.ToUnixTimeSeconds()}:R>", true)
            .WithTimestamp(DateTimeOffset.UtcNow);

        await channel.SendMessageAsync(new DiscordMessageBuilder().AddEmbed(embed));
    }

    private static async Task SendChannelLogAsync(
        DiscordGuild guild,
        AutoModerationConfig config,
        DiscordMember member,
        DiscordChannel sourceChannel,
        string reason,
        string ruleKey)
    {
        if (config.LogChannelId is null || config.LogChannelId.Value == 0)
        {
            return;
        }

        if (!guild.Channels.TryGetValue(config.LogChannelId.Value, out var logChannel))
        {
            return;
        }

        var embed = SectorUI.CreateLogEmbed(
                $"{BotEmojis.Warning} AutoMod Block",
                $"Message from {member.Mention} was blocked in {sourceChannel.Mention}.",
                SectorUI.SectorWarning,
                $"User ID: {member.Id} | Channel ID: {sourceChannel.Id}")
            .AddField("Rule", ruleKey, true)
            .AddField("Reason", reason)
            .WithTimestamp(DateTimeOffset.UtcNow);

        await logChannel.SendMessageAsync(new DiscordMessageBuilder().AddEmbed(embed));
    }
}
