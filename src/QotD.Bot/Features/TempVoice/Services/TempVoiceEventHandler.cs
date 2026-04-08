using System.Collections.Concurrent;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using DSharpPlus.Interactivity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using QotD.Bot.Data;

namespace QotD.Bot.Features.TempVoice.Services;

public sealed class TempVoiceEventHandler :
    IEventHandler<VoiceStateUpdatedEventArgs>,
    IEventHandler<ComponentInteractionCreatedEventArgs>
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<TempVoiceEventHandler> _logger;
    private readonly string _instanceId = Guid.NewGuid().ToString("N")[..8];

    // channelId -> ownerId
    private readonly ConcurrentDictionary<ulong, ulong> _tempChannels = new();

    public TempVoiceEventHandler(IServiceProvider serviceProvider, ILogger<TempVoiceEventHandler> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _logger.LogInformation("TempVoice handler initialized. InstanceId={InstanceId}", _instanceId);
    }

    public bool IsOwner(ulong channelId, ulong userId)
        => _tempChannels.TryGetValue(channelId, out var ownerId) && ownerId == userId;

    public async Task HandleEventAsync(DiscordClient client, VoiceStateUpdatedEventArgs e)
    {
        _logger.LogInformation("VOICE_EVENT[{InstanceId}]: User {UserId}, Guild {GuildId}, Before: {BeforeId}, After: {AfterId}", 
            _instanceId,
            e.After?.UserId ?? e.Before?.UserId, 
            e.After?.GuildId ?? e.Before?.GuildId,
            e.Before?.ChannelId, 
            e.After?.ChannelId);

        var beforeChannelId = e.Before?.ChannelId ?? 0;
        var afterChannelId = e.After?.ChannelId ?? 0;
        var userId = e.Before?.UserId ?? e.After?.UserId ?? 0;
        var guildId = e.Before?.GuildId ?? e.After?.GuildId ?? 0;

        if (userId == 0 || guildId == 0) return;

        // 1. Handle user leaving a temp channel (cleanup)
        var shouldAttemptCleanup = false;
        if (beforeChannelId != 0)
        {
            shouldAttemptCleanup = _tempChannels.ContainsKey(beforeChannelId)
                                 || await IsManagedTempChannelAsync(client, guildId, beforeChannelId);
        }

        if (shouldAttemptCleanup)
        {
            _logger.LogInformation("Cleaning up channel {ChannelId} because user {UserId} left. (Gate before: {Before}, After: {After}) [InstanceId={InstanceId}]", 
                beforeChannelId, userId, beforeChannelId, afterChannelId, _instanceId);
            
            // Give the cache a bit more time to update
            await Task.Delay(1000);

            var cleanedUp = await TryCleanupTempChannelAsync(client, guildId, beforeChannelId, userId, afterChannelId);
            if (!cleanedUp && _tempChannels.ContainsKey(beforeChannelId))
            {
                _logger.LogInformation(
                    "Retrying cleanup for temp channel {ChannelId} after short delay. [InstanceId={InstanceId}]",
                    beforeChannelId,
                    _instanceId);

                await Task.Delay(2500);
                await TryCleanupTempChannelAsync(client, guildId, beforeChannelId, userId, afterChannelId);
            }
        }
        else if (beforeChannelId != 0 && afterChannelId != beforeChannelId)
        {
            _logger.LogDebug(
                "Skip cleanup for channel {ChannelId}: not tracked and not recognized as managed temp voice. [InstanceId={InstanceId}]",
                beforeChannelId,
                _instanceId);
        }

        // 2. Handle user joining the trigger channel
        if (afterChannelId == 0) return;

        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var config = await db.TempVoiceConfigs.FirstOrDefaultAsync(c => c.GuildId == guildId);
        if (config == null || afterChannelId != config.TriggerChannelId) return;

        try
        {
            var guild = await client.GetGuildAsync(guildId);
            var member = await guild.GetMemberAsync(userId);

            // Determine parent category
            DiscordChannel? parent = null;
            if (config.CategoryId.HasValue)
                parent = guild.Channels.GetValueOrDefault(config.CategoryId.Value);
            if (parent == null)
            {
                var triggerChannel = guild.Channels.GetValueOrDefault(config.TriggerChannelId);
                parent = triggerChannel?.Parent;
            }

            // Create temp channel
            var newChannel = await guild.CreateChannelAsync(
                $"🔊 {member.DisplayName}'s Channel",
                DiscordChannelType.Voice,
                parent);

            _tempChannels[newChannel.Id] = userId;
            _logger.LogInformation(
                "Tracking temp channel {ChannelId} owner {OwnerId}. trackedCount={TrackedCount} [InstanceId={InstanceId}]",
                newChannel.Id,
                userId,
                _tempChannels.Count,
                _instanceId);

            // Move user
            await member.ModifyAsync(m => m.VoiceChannel = newChannel);

            _logger.LogInformation("Created temp channel for {User} in {Guild} [InstanceId={InstanceId}]", member.DisplayName, guild.Name, _instanceId);

            // Send control panel
            await SendControlPanelAsync(newChannel, member);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create temp voice channel for user {UserId} [InstanceId={InstanceId}]", userId, _instanceId);
        }
    }

    public async Task HandleEventAsync(DiscordClient client, ComponentInteractionCreatedEventArgs e)
    {
        if (!e.Id.StartsWith("tv_")) return;

        var member = (DiscordMember)e.User;
        if (member == null) return;

        var voiceState = member.VoiceState;
        var voiceChannelId = voiceState?.ChannelId ?? 0;

        if (voiceChannelId == 0)
        {
            await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder().WithContent("❌ You are not in a voice channel.").AsEphemeral());
            return;
        }

        if (!_tempChannels.TryGetValue(voiceChannelId, out var ownerId) || ownerId != member.Id)
        {
            await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder().WithContent("❌ You are not the owner of this channel.").AsEphemeral());
            return;
        }

        var voiceChannel = await client.GetChannelAsync(voiceChannelId);

        switch (e.Id)
        {
            case "tv_rename":
                await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                        .WithContent("📝 Send the new channel name in this chat (30s timeout):")
                        .AsEphemeral());

                var interactivity = client.ServiceProvider.GetRequiredService<InteractivityExtension>();
                var nameResponse = await interactivity.WaitForMessageAsync(
                    m => m.Author.Id == member.Id && m.ChannelId == e.Channel.Id,
                    TimeSpan.FromSeconds(30));

                if (!nameResponse.TimedOut && nameResponse.Result != null)
                {
                    var newName = nameResponse.Result.Content;
                    if (newName.Length > 100) newName = newName[..100];
                    await voiceChannel.ModifyAsync(c => c.Name = $"🔊 {newName}");
                    try { await nameResponse.Result.DeleteAsync(); } catch { }
                    await e.Interaction.EditOriginalResponseAsync(
                        new DiscordWebhookBuilder().WithContent($"✅ Channel renamed to **{newName}**"));
                }
                else
                {
                    await e.Interaction.EditOriginalResponseAsync(
                        new DiscordWebhookBuilder().WithContent("⏰ Timed out."));
                }
                break;

            case "tv_limit":
                await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                        .WithContent("🔢 Send the user limit (0 = unlimited, max 99):")
                        .AsEphemeral());

                var limitInteractivity = client.ServiceProvider.GetRequiredService<InteractivityExtension>();
                var limitResponse = await limitInteractivity.WaitForMessageAsync(
                    m => m.Author.Id == member.Id && m.ChannelId == e.Channel.Id,
                    TimeSpan.FromSeconds(30));

                if (!limitResponse.TimedOut && limitResponse.Result != null && int.TryParse(limitResponse.Result.Content, out var limit))
                {
                    limit = Math.Clamp(limit, 0, 99);
                    // await voiceChannel.ModifyAsync(c => c.UserLimit = limit);
                    try { await limitResponse.Result.DeleteAsync(); } catch { }
                    await e.Interaction.EditOriginalResponseAsync(
                        new DiscordWebhookBuilder().WithContent(limit > 0 ? $"✅ User limit set to **{limit}**" : "✅ User limit removed"));
                }
                else
                {
                    await e.Interaction.EditOriginalResponseAsync(
                        new DiscordWebhookBuilder().WithContent("⏰ Timed out or invalid number."));
                }
                break;

            case "tv_lock":
                await voiceChannel.AddOverwriteAsync(e.Guild.EveryoneRole, deny: DiscordPermission.Connect);
                await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder().WithContent("🔒 Channel **locked**. No one else can join.").AsEphemeral());
                break;

            case "tv_unlock":
                await voiceChannel.AddOverwriteAsync(e.Guild.EveryoneRole, allow: DiscordPermission.Connect);
                await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder().WithContent("🔓 Channel **unlocked**. Everyone can join again.").AsEphemeral());
                break;
        }
    }

    private async Task<bool> TryCleanupTempChannelAsync(
        DiscordClient client,
        ulong guildId,
        ulong channelId,
        ulong leaverUserId,
        ulong afterChannelId)
    {
        try
        {
            var guild = await client.GetGuildAsync(guildId);
            var oldChannel = guild.Channels.GetValueOrDefault(channelId);

            if (oldChannel == null)
            {
                _logger.LogInformation("Temp channel {ChannelId} already gone. [InstanceId={InstanceId}]", channelId, _instanceId);
                _tempChannels.TryRemove(channelId, out _);
                return true;
            }

            var usersInChannel = oldChannel.Users;
            var userCount = usersInChannel.Count;
            var onlyLeaverRemainsInCache =
                userCount == 1 &&
                usersInChannel.Any(u => u.Id == leaverUserId) &&
                afterChannelId != channelId;

            _logger.LogInformation(
                "Temp channel {ChannelId} currently has {Count} users. onlyLeaverRemainsInCache={OnlyLeaver} [InstanceId={InstanceId}]",
                channelId,
                userCount,
                onlyLeaverRemainsInCache,
                _instanceId);

            if (userCount != 0 && !onlyLeaverRemainsInCache)
                return false;

            await oldChannel.DeleteAsync("Temp voice channel empty");
            _tempChannels.TryRemove(channelId, out _);
            _logger.LogInformation("Successfully deleted empty temp channel {ChannelId} [InstanceId={InstanceId}]", channelId, _instanceId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cleanup temp channel {ChannelId} [InstanceId={InstanceId}]", channelId, _instanceId);
            if (ex.Message.Contains("404"))
            {
                _tempChannels.TryRemove(channelId, out _);
                return true;
            }

            return false;
        }
    }

    private async Task<bool> IsManagedTempChannelAsync(DiscordClient client, ulong guildId, ulong channelId)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var config = await db.TempVoiceConfigs.FirstOrDefaultAsync(c => c.GuildId == guildId);
            if (config == null || channelId == config.TriggerChannelId)
                return false;

            var guild = await client.GetGuildAsync(guildId);
            var channel = guild.Channels.GetValueOrDefault(channelId);

            if (channel == null || channel.Type != DiscordChannelType.Voice)
                return false;

            var channelParentId = channel.Parent?.Id;

            if (config.CategoryId.HasValue)
                return channelParentId == config.CategoryId.Value;

            var triggerChannel = guild.Channels.GetValueOrDefault(config.TriggerChannelId);
            if (triggerChannel == null)
                return false;

            var triggerParentId = triggerChannel.Parent?.Id;
            var sameParent = channelParentId != null && channelParentId == triggerParentId;

            return sameParent && channel.Name.StartsWith("🔊 ", StringComparison.Ordinal);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to resolve whether channel {ChannelId} is managed temp voice. [InstanceId={InstanceId}]", channelId, _instanceId);
            return false;
        }
    }

    private static async Task SendControlPanelAsync(DiscordChannel channel, DiscordMember owner)
    {
        var embed = new DiscordEmbedBuilder()
            .WithTitle("🎛️ Voice Channel Control Panel")
            .WithDescription($"Owner: {owner.Mention}\n\nUse the buttons below to manage your channel.")
            .WithColor(DiscordColor.Blurple)
            .Build();

        var renameBtn = new DiscordButtonComponent(DiscordButtonStyle.Primary, "tv_rename", "✏️ Rename");
        var limitBtn = new DiscordButtonComponent(DiscordButtonStyle.Primary, "tv_limit", "🔢 Limit");
        var lockBtn = new DiscordButtonComponent(DiscordButtonStyle.Danger, "tv_lock", "🔒 Lock");
        var unlockBtn = new DiscordButtonComponent(DiscordButtonStyle.Success, "tv_unlock", "🔓 Unlock");

        var builder = new DiscordMessageBuilder()
            .AddEmbed(embed)
            .AddActionRowComponent(new DiscordActionRowComponent(new DiscordComponent[] { renameBtn, limitBtn, lockBtn, unlockBtn }));

        await channel.SendMessageAsync(builder);
    }
}
