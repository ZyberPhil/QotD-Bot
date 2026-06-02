using System.ComponentModel;
using DSharpPlus.Commands;
using DSharpPlus.Entities;
using QotD.Bot.Features.Economy.Services;
using QotD.Bot.UI;

namespace QotD.Bot.Features.Economy.Commands;

public sealed class BalanceCommand
{
    private readonly IEconomyService _economyService;

    public BalanceCommand(IEconomyService economyService)
    {
        _economyService = economyService;
    }

    [Command("balance")]
    [Description("Zeigt das Guthaben von dir oder einem anderen Nutzer an.")]
    public async ValueTask ExecuteAsync(CommandContext ctx, [Description("Optionaler Nutzer")] DiscordUser? user = null)
    {
        var target = user ?? ctx.User;
        var result = await _economyService.GetBalanceAsync(target.Id);

        if (!result.IsSuccess)
        {
            await ctx.RespondAsync($"❌ {result.ErrorMessage}");
            return;
        }

        var embed = SectorUI.CreateBaseEmbed(
            "💰 Wallet",
            $"**{target.Username}** hat aktuell **{result.Balance:N0}** Coins.")
            .WithColor(SectorUI.SectorSuccessGreen)
            .WithUserThumbnail(target)
            .WithStandardFooter("Economy");

        await ctx.RespondAsync(new DiscordMessageBuilder().AddEmbed(embed));
    }
}