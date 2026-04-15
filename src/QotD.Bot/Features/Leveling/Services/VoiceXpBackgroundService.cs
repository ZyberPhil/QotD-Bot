using DSharpPlus;
using DSharpPlus.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using QotD.Bot.UI;

namespace QotD.Bot.Features.Leveling.Services;

public sealed class VoiceXpBackgroundService(
    IServiceProvider serviceProvider,
    LevelService levelService,
    ILogger<VoiceXpBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromMinutes(1);
    private DiscordClient Discord => serviceProvider.GetRequiredService<DiscordClient>();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Voice XP background service started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await AwardVoiceXpAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error while awarding voice XP.");
            }

            await Task.Delay(TickInterval, stoppingToken);
        }

        logger.LogInformation("Voice XP background service stopped.");
    }

    private async Task AwardVoiceXpAsync(CancellationToken ct)
    {
        var nowUtc = DateTimeOffset.UtcNow;

        foreach (var guild in Discord.Guilds.Values)
        {
            if (ct.IsCancellationRequested)
            {
                return;
            }

            var voiceSettings = await levelService.GetVoiceXpSettingsAsync(guild.Id);

            var activeHumansPerChannel = guild.Members.Values
                .Where(member => IsActiveHumanInVoice(member, voiceSettings))
                .GroupBy(m => m.VoiceState!.ChannelId ?? 0)
                .Where(g => g.Key != 0)
                .ToDictionary(g => g.Key, g => g.Count());

            foreach (var member in guild.Members.Values)
            {
                if (ct.IsCancellationRequested)
                {
                    return;
                }

                if (!IsEligibleForVoiceXp(member, activeHumansPerChannel, voiceSettings))
                {
                    continue;
                }

                await levelService.TrackVoiceMinuteAsync(guild.Id, member.Id, nowUtc);

                var result = await levelService.GrantVoiceXpAsync(guild.Id, member.Id, nowUtc);
                if (!result.LeveledUp)
                {
                    continue;
                }

                await TrySendLevelUpMessageAsync(guild, member, result);
            }
        }
    }

    private static bool IsActiveHumanInVoice(DiscordMember member, VoiceXpSettings settings)
    {
        if (member.IsBot || member.VoiceState is null)
        {
            return false;
        }

        if (member.VoiceState.ChannelId == 0)
        {
            return false;
        }

        if (!settings.AllowSelfMutedOrDeafened &&
            (member.VoiceState.IsSelfMuted || member.VoiceState.IsSelfDeafened))
        {
            return false;
        }

        return true;
    }

    private static bool IsEligibleForVoiceXp(
        DiscordMember member,
        IReadOnlyDictionary<ulong, int> activeHumansPerChannel,
        VoiceXpSettings settings)
    {
        if (!IsActiveHumanInVoice(member, settings))
        {
            return false;
        }

        var channelId = member.VoiceState!.ChannelId ?? 0;
        if (channelId == 0)
        {
            return false;
        }

        if (!activeHumansPerChannel.TryGetValue(channelId, out var activeCount))
        {
            return false;
        }

        if (activeCount < settings.MinActiveUsers)
        {
            return false;
        }

        return true;
    }

    private async Task TrySendLevelUpMessageAsync(DiscordGuild guild, DiscordMember member, LevelGrantResult result)
    {
        var levelUpChannelId = await levelService.GetLevelUpChannelAsync(guild.Id);
        if (levelUpChannelId == 0)
        {
            return;
        }

        if (!guild.Channels.TryGetValue((ulong)levelUpChannelId, out var channel))
        {
            return;
        }

        var embed = new DiscordEmbedBuilder()
            .WithTitle("Level Up")
            .WithColor(CozyCoveUI.CozySuccessGreen)
            .WithDescription($"{member.Mention} hat Level **{result.NewLevel}** erreicht!\n+{result.GainedXp} XP")
            .WithTimestamp(DateTimeOffset.UtcNow);

        await channel.SendMessageAsync(new DiscordMessageBuilder().AddEmbed(embed));
    }
}