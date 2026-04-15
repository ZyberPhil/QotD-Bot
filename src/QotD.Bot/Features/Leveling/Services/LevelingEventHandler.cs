using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using QotD.Bot.UI;

namespace QotD.Bot.Features.Leveling.Services;

public sealed class LevelingEventHandler : IEventHandler<MessageCreatedEventArgs>
{
    private readonly LevelService _levelService;

    public LevelingEventHandler(LevelService levelService)
    {
        _levelService = levelService;
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

        var result = await _levelService.GrantMessageXpAsync(e.Guild.Id, e.Author.Id, DateTimeOffset.UtcNow);
        if (!result.LeveledUp)
        {
            return;
        }

        var levelUpChannelId = await _levelService.GetLevelUpChannelAsync(e.Guild.Id);
        if (levelUpChannelId == 0)
        {
            return;
        }

        if (!e.Guild.Channels.TryGetValue((ulong)levelUpChannelId, out var channel))
        {
            return;
        }

        var bannerUrl = await _levelService.GetLevelUpBannerUrlAsync(e.Guild.Id);

        var embed = new DiscordEmbedBuilder()
            .WithTitle("Level Up")
            .WithColor(SectorUI.SectorSuccessGreen)
            .WithDescription($"{e.Author.Mention} hat Level **{result.NewLevel}** erreicht!\n+{result.GainedXp} XP")
            .WithTimestamp(DateTimeOffset.UtcNow);

        if (!string.IsNullOrWhiteSpace(e.Author.AvatarUrl))
        {
            embed.WithThumbnail(e.Author.AvatarUrl);
        }

        if (!string.IsNullOrWhiteSpace(bannerUrl))
        {
            embed.WithImageUrl(bannerUrl);
        }

        await channel.SendMessageAsync(new DiscordMessageBuilder().AddEmbed(embed));
    }
}
