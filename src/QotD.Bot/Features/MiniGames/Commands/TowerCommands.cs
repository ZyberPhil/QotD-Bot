using System.ComponentModel;
using DSharpPlus.Commands;
using QotD.Bot.Features.Economy.Services;
using QotD.Bot.Features.MiniGames.Services;
using QotD.Bot.UI;

namespace QotD.Bot.Features.MiniGames.Commands;

public class TowerCommands
{
    private readonly TowerService _towerService;
    private readonly EconomyService _economyService;

    public TowerCommands(TowerService towerService, EconomyService economyService)
    {
        _towerService = towerService;
        _economyService = economyService;
    }

    [Command("tower")]
    [Description("Erklimme den Turm und vervielfache deinen Gewinn!")]
    public async ValueTask PlayAsync(CommandContext ctx, 
        [Description("Dein Einsatz (Coins)")] int bet = 100)
    {
        var userLock = _towerService.GetLock(ctx.User.Id);
        await userLock.WaitAsync();

        try
        {
            bool apiOffline = false;
            if (bet > 0)
            {
                var economyResult = await _economyService.RemoveCoinsAsync(ctx.User.Id, bet);
                if (!economyResult.IsApiAvailable)
                {
                    bet = 0;
                    apiOffline = true;
                }
                else if (!economyResult.IsSuccess)
                {
                    await ctx.RespondAsync($"❌ {economyResult.ErrorMessage}");
                    return;
                }
            }

            var game = _towerService.StartGame(ctx.User.Id, bet);
            var response = TowerUI.BuildResponse(game);
            if (apiOffline) response.WithContent("⚠️ Die Economy-API ist derzeit offline. Das Spiel startet ohne Echtgeld-Einsatz! (Just for Fun)");
            await ctx.RespondAsync(response);
        }
        catch (Exception)
        {
            await ctx.RespondAsync("Ein technischer Fehler ist aufgetreten.");
        }
        finally
        {
            userLock.Release();
        }
    }
}
