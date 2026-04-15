using System.ComponentModel;
using DSharpPlus.Commands;
using DSharpPlus.Entities;
using QotD.Bot.Features.Leveling.Services;
using QotD.Bot.UI;

namespace QotD.Bot.Features.Leveling.Commands;

public sealed class LevelModule
{
    private readonly LevelService _levelService;

    public LevelModule(LevelService levelService)
    {
        _levelService = levelService;
    }

    [Command("rank")]
    [Description("Zeigt den Rang, das Level und den XP-Fortschritt an.")]
    public async ValueTask RankAsync(CommandContext ctx, [Description("Optionaler Benutzer")] DiscordUser? user = null)
    {
        if (ctx.Guild is null)
        {
            await ctx.RespondAsync("Dieser Befehl kann nur in einem Server genutzt werden.");
            return;
        }

        var target = user ?? ctx.User;
        var snapshot = await _levelService.GetUserSnapshotAsync(ctx.Guild.Id, target.Id);

        var progressPercent = snapshot.RequiredLevelXp == 0
            ? 0
            : (int)Math.Round((double)snapshot.CurrentLevelXp / snapshot.RequiredLevelXp * 100, MidpointRounding.AwayFromZero);

        var embed = new DiscordEmbedBuilder()
            .WithTitle($"Rank von {target.Username}")
            .WithColor(SectorUI.SectorPrimary)
            .WithDescription($"**Level:** {snapshot.Level}\n**Gesamt-XP:** {snapshot.TotalXp}")
            .AddField("Rang", snapshot.Rank > 0 ? $"#{snapshot.Rank}" : "Unplatziert", true)
            .AddField("Fortschritt", $"{snapshot.CurrentLevelXp}/{snapshot.RequiredLevelXp} XP ({progressPercent}%)", true)
            .AddField("Nachrichten", snapshot.MessageCount.ToString(), true)
            .WithUserThumbnail(target)
            .WithTimestamp(DateTimeOffset.UtcNow);

        await ctx.RespondAsync(new DiscordMessageBuilder().AddEmbed(embed));
    }

    [Command("leaderboard")]
    [Description("Zeigt die Top 10 des Server-Levelings.")]
    public async ValueTask LeaderboardAsync(CommandContext ctx)
    {
        if (ctx.Guild is null)
        {
            await ctx.RespondAsync("Dieser Befehl kann nur in einem Server genutzt werden.");
            return;
        }

        var top = await _levelService.GetLeaderboardAsync(ctx.Guild.Id, 10);
        if (top.Count == 0)
        {
            await ctx.RespondAsync("Noch keine Level-Daten auf diesem Server vorhanden.");
            return;
        }

        var lines = top.Select(entry =>
            $"**#{entry.Rank}** <@{entry.Snapshot.UserId}> - Level {entry.Snapshot.Level} ({entry.Snapshot.TotalXp} XP)");

        var embed = new DiscordEmbedBuilder()
            .WithTitle("Level Leaderboard")
            .WithColor(SectorUI.SectorGold)
            .WithDescription(string.Join("\n", lines))
            .WithTimestamp(DateTimeOffset.UtcNow);

        await ctx.RespondAsync(new DiscordMessageBuilder().AddEmbed(embed));
    }
}
