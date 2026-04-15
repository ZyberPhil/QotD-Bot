using System.IO;
using System.Linq;
using DSharpPlus;
using DSharpPlus.Entities;
using QotD.Bot.Features.MiniGames.Models;

namespace QotD.Bot.UI;

public static class BlackjackUI
{

    public static DiscordInteractionResponseBuilder BuildResponse(BlackjackGame game, byte[] imageBytes, bool showButtons = true)
    {
        var builder = new DiscordInteractionResponseBuilder();

        var embed = SectorUI.CreateBaseEmbed("🃏 Blackjack", GetStatusMessage(game))
            .WithImageUrl("attachment://blackjack.png")
            .AddField("Dealer", (game.Status == GameStatus.Playing || game.Status == GameStatus.PlayerBust || game.Status == GameStatus.PlayerBlackjack) ? "?" : game.DealerValue.ToString(), true)
            .AddField("Player Hand", game.PlayerValue.ToString(), true);

        builder.AddEmbed(embed);
        builder.AddFile("blackjack.png", new MemoryStream(imageBytes));

        if (showButtons && game.Status == GameStatus.Playing)
        {
            builder.AddActionRowComponent(new DiscordActionRowComponent(new DiscordComponent[] {
                new DiscordButtonComponent(DiscordButtonStyle.Primary, $"bj_hit_{game.UserId}", "Hit"),
                new DiscordButtonComponent(DiscordButtonStyle.Secondary, $"bj_stand_{game.UserId}", "Stand")
            }));
        }
        else if (showButtons)
        {
            builder.AddActionRowComponent(new DiscordActionRowComponent(new DiscordComponent[] {
                new DiscordButtonComponent(DiscordButtonStyle.Success, $"bj_play_again_{game.UserId}_{game.Bet}", "Play Again")
            }));
        }

        return builder;
    }

    private static string GetStatusMessage(BlackjackGame game)
    {
        return game.Status switch
        {
            GameStatus.Playing => "Mache deinen nächsten Zug. Hit für eine weitere Karte, Stand zum Halten.",
            GameStatus.PlayerBust => "Bust! Du hast mehr als 21. Dealer gewinnt.",
            GameStatus.DealerBust => "Dealer Bust! Der Dealer hat mehr als 21. Du gewinnst!",
            GameStatus.PlayerBlackjack => "Blackjack! Du hast 21 mit den ersten zwei Karten. Du gewinnst!",
            GameStatus.DealerBlackjack => "Dealer Blackjack! Der Dealer hat 21. Dealer gewinnt.",
            GameStatus.PlayerWon => "Du hast gewonnen! Dein Blatt ist näher an 21.",
            GameStatus.DealerWon => "Dealer gewinnt. Das Blatt des Dealers ist näher an 21.",
            GameStatus.Push => "Unentschieden (Push). Keiner gewinnt.",
            _ => "Unbekannter Fehler."
        };
    }

    public static DiscordWebhookBuilder ToWebhookBuilder(this DiscordInteractionResponseBuilder builder)
    {
        var webhookBuilder = new DiscordWebhookBuilder();
        foreach (var embed in builder.Embeds) webhookBuilder.AddEmbed(embed);
        foreach (var file in builder.Files) webhookBuilder.AddFile(file.FileName, file.Stream);
        
        foreach (var row in builder.Components)
        {
            if (row is DiscordActionRowComponent actionRow)
            {
                webhookBuilder.AddActionRowComponent(actionRow);
            }
        }
        return webhookBuilder;
    }
}
