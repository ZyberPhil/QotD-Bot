using System.ComponentModel;
using DSharpPlus.Commands;
using DSharpPlus.Entities;
using QotD.Bot.Data.Models;
using QotD.Bot.Features.Economy.Services;
using QotD.Bot.UI;

namespace QotD.Bot.Features.Economy.Commands;

public sealed class LedgerCommand
{
    private readonly IEconomyService _economyService;

    public LedgerCommand(IEconomyService economyService)
    {
        _economyService = economyService;
    }

    [Command("ledger")]
    [Description("Zeigt die letzten Coin-Transaktionen an.")]
    public async ValueTask ExecuteAsync(
        CommandContext ctx,
        [Description("Optionaler Nutzer")] DiscordUser? user = null,
        [Description("Seite (ab 1)")] int page = 1)
    {
        var target = user ?? ctx.User;
        page = Math.Max(page, 1);
        const int pageSize = 5;

        var entries = await _economyService.GetLedgerAsync(target.Id, pageSize * page);
        var pageEntries = entries.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        if (pageEntries.Count == 0)
        {
            await ctx.RespondAsync("Noch keine Transaktionen vorhanden.");
            return;
        }

        var lines = pageEntries.Select(FormatEntry);
        var embed = SectorUI.CreateBaseEmbed(
            "📒 Ledger",
            $"Letzte Transaktionen von **{target.Username}** (Seite {page}).")
            .WithColor(SectorUI.SectorPrimary)
            .WithUserThumbnail(target)
            .AddField("Einträge", string.Join("\n", lines), false)
            .WithStandardFooter("Economy Ledger");

        await ctx.RespondAsync(new DiscordMessageBuilder().AddEmbed(embed));
    }

    private static string FormatEntry(EconomyLedgerEntry entry)
    {
        var sign = entry.Amount >= 0 ? "+" : string.Empty;
        var type = entry.TransactionType switch
        {
            EconomyTransactionType.InitialGrant => "Start",
            EconomyTransactionType.Credit => "Credit",
            EconomyTransactionType.Debit => "Debit",
            EconomyTransactionType.Adjustment => "Adjust",
            _ => entry.TransactionType.ToString()
        };

        var reason = string.IsNullOrWhiteSpace(entry.Reason) ? "ohne Notiz" : entry.Reason;
        return $"`{entry.CreatedAtUtc:MM-dd HH:mm}` {type} {sign}{entry.Amount:N0} → {entry.BalanceAfter:N0} | {reason}";
    }
}