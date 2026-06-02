using System.ComponentModel;
using DSharpPlus.Commands;
using QotD.Bot.Features.Economy.Services;
using QotD.Bot.Features.MiniGames.Services;
using QotD.Bot.UI;

namespace QotD.Bot.Features.MiniGames.Commands;

public class TowerCommands
{
    private readonly TowerService _towerService;
    private readonly IEconomyService _economyService;

    public TowerCommands(TowerService towerService, IEconomyService economyService)
    {
        _towerService = towerService;
        _economyService = economyService;
    }

    [Command("tower")]
    [Description("Erklimme den Turm und vervielfache deinen Gewinn!")]
    public async ValueTask PlayAsync(CommandContext ctx, 
        [Description("Dein Einsatz (Coins)")] int bet = 100)
    {
        if (ctx.Guild is null)
        {
            await ctx.RespondAsync("Dieser Befehl kann nur in einem Server verwendet werden.");
            return;
        }

        var guildId = ctx.Guild.Id;
        var userLock = _towerService.GetLock(guildId, ctx.User.Id);
        await userLock.WaitAsync();
        var betReserved = false;

        try
        {
            if (bet > 0)
            {
                var economyResult = await _economyService.RemoveCoinsAsync(ctx.User.Id, bet);
                if (!economyResult.IsSuccess)
                {
                    await ctx.RespondAsync($"❌ {economyResult.ErrorMessage}");
                    return;
                }

                betReserved = true;
            }

            var game = _towerService.StartGame(guildId, ctx.User.Id, bet);
            var response = TowerUI.BuildResponse(game);
            await ctx.RespondAsync(response);
        }
        catch (Exception)
        {
            if (betReserved && bet > 0)
            {
                await _economyService.AddCoinsAsync(ctx.User.Id, bet);
            }

            await ctx.RespondAsync("Ein technischer Fehler ist aufgetreten.");
        }
        finally
        {
            userLock.Release();
        }
    }
}
