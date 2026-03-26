using System.ComponentModel;
using DSharpPlus.Commands;
using QotD.Bot.Features.MiniGames.Services;
using QotD.Bot.UI;

namespace QotD.Bot.Features.MiniGames.Commands;

public class TowerCommands
{
    private readonly TowerService _towerService;

    public TowerCommands(TowerService towerService)
    {
        _towerService = towerService;
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
            var game = _towerService.StartGame(ctx.User.Id, bet);
            var response = TowerUI.BuildResponse(game);
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
