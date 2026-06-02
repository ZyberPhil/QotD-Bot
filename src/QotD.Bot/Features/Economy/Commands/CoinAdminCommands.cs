using System.ComponentModel;
using DSharpPlus.Commands;
using DSharpPlus.Commands.ContextChecks;
using DSharpPlus.Entities;
using QotD.Bot.Features.Economy.Models;
using QotD.Bot.Features.Economy.Services;
using QotD.Bot.UI;

namespace QotD.Bot.Features.Economy.Commands;

[Command("coins")]
[Description("Verwaltungsbefehle für Coins.")]
public sealed class CoinAdminCommands
{
    private readonly IEconomyService _economyService;

    public CoinAdminCommands(IEconomyService economyService)
    {
        _economyService = economyService;
    }

    [Command("add")]
    [RequirePermissions(DiscordPermission.ManageGuild)]
    [Description("Gibt einem Nutzer Coins.")]
    public async ValueTask AddAsync(
        CommandContext ctx,
        [Description("Zielnutzer")] DiscordUser user,
        [Description("Menge")] int amount,
        [Description("Grund")] string? reason = null)
    {
        var result = await _economyService.AddCoinsAsync(user.Id, amount, ctx.User.Id, reason ?? "Admin add");
        await RespondAsync(ctx, result, user, amount, "hinzugefügt");
    }

    [Command("remove")]
    [RequirePermissions(DiscordPermission.ManageGuild)]
    [Description("Entfernt Coins von einem Nutzer.")]
    public async ValueTask RemoveAsync(
        CommandContext ctx,
        [Description("Zielnutzer")] DiscordUser user,
        [Description("Menge")] int amount,
        [Description("Grund")] string? reason = null)
    {
        var result = await _economyService.RemoveCoinsAsync(user.Id, amount, ctx.User.Id, reason ?? "Admin remove");
        await RespondAsync(ctx, result, user, amount, "entfernt");
    }

    [Command("set")]
    [RequirePermissions(DiscordPermission.ManageGuild)]
    [Description("Setzt das Guthaben eines Nutzers auf einen festen Wert.")]
    public async ValueTask SetAsync(
        CommandContext ctx,
        [Description("Zielnutzer")] DiscordUser user,
        [Description("Neuer Kontostand")] long amount,
        [Description("Grund")] string? reason = null)
    {
        var result = await _economyService.SetBalanceAsync(user.Id, amount, ctx.User.Id, reason ?? "Admin set");
        if (!result.IsSuccess)
        {
            await ctx.RespondAsync($"❌ {result.ErrorMessage}");
            return;
        }

        await ctx.RespondAsync($"✅ Guthaben von {user.Mention} auf **{amount:N0}** Coins gesetzt.");
    }

    private static async Task RespondAsync(CommandContext ctx, EconomyResult result, DiscordUser user, int amount, string action)
    {
        if (!result.IsSuccess)
        {
            await ctx.RespondAsync($"❌ {result.ErrorMessage}");
            return;
        }

        var embed = SectorUI.CreateSuccessEmbed(
            $"**{amount:N0}** Coins wurden {action}.",
            $"Coins {action}")
            .AddField("Nutzer", user.Mention, true)
            .AddField("Kontostand", $"{result.Balance:N0}", true)
            .WithStandardFooter("Economy Admin");

        await ctx.RespondAsync(new DiscordMessageBuilder().AddEmbed(embed));
    }
}