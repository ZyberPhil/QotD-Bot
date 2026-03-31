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

    // channelId -> ownerId
    private readonly ConcurrentDictionary<ulong, ulong> _tempChannels = new();

    public TempVoiceEventHandler(IServiceProvider serviceProvider, ILogger<TempVoiceEventHandler> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public bool IsOwner(ulong channelId, ulong userId)
        => _tempChannels.TryGetValue(channelId, out var ownerId) && ownerId == userId;

    public async Task HandleEventAsync(DiscordClient client, VoiceStateUpdatedEventArgs e)
    {
        var beforeChannelId = e.Before?.ChannelId ?? 0;
        var afterChannelId = e.After?.ChannelId ?? 0;
        var userId = e.Before?.UserId ?? e.After?.UserId ?? 0;
        var guildId = e.Before?.GuildId ?? e.After?.GuildId ?? 0;

        if (userId == 0 || guildId == 0) return;

        // 1. Handle user leaving a temp channel (cleanup)
        if (beforeChannelId != 0 && _tempChannels.ContainsKey(beforeChannelId))
        {
            _logger.LogDebug("User {UserId} left potentially temp channel {ChannelId}", userId, beforeChannelId);
            
            // Give the cache a moment to update user counts
            await Task.Delay(500);

            try
            {
                var oldChannel = await client.GetChannelAsync(beforeChannelId);
                var userCount = oldChannel.Users.Count;
                
                _logger.LogDebug("Temp channel {ChannelId} currently has {Count} users", beforeChannelId, userCount);

                if (userCount == 0)
                {
                    _tempChannels.TryRemove(beforeChannelId, out _);
                    await oldChannel.DeleteAsync("Temp voice channel empty");
                    _logger.LogInformation("Deleted empty temp channel {ChannelId}", beforeChannelId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to cleanup temp channel {ChannelId}", beforeChannelId);
                // If it fails (e.g. 404), still try to remove from dictionary if we suspect it's gone
                if (ex.Message.Contains("404"))
                {
                   _tempChannels.TryRemove(beforeChannelId, out _);
                }
            }
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

            // Move user
            await member.ModifyAsync(m => m.VoiceChannel = newChannel);

            _logger.LogInformation("Created temp channel for {User} in {Guild}", member.DisplayName, guild.Name);

            // Send control panel
            await SendControlPanelAsync(newChannel, member);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create temp voice channel for user {UserId}", userId);
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
