using DSharpPlus;
using DSharpPlus.Entities;
using QotD.Bot.Features.MiniGames.Models;

namespace QotD.Bot.UI;

public static class BlackjackUI
{

    public static DiscordInteractionResponseBuilder BuildResponse(BlackjackGame game, byte[] imageBytes)
    {
        var builder = new DiscordInteractionResponseBuilder();

        var embed = CozyCoveUI.CreateBaseEmbed("🃏 Blackjack", GetStatusMessage(game))
            .WithImageUrl("attachment://blackjack.png")
            .AddField("Dealer", (game.Status == GameStatus.Playing || game.Status == GameStatus.PlayerBust || game.Status == GameStatus.PlayerBlackjack) ? "?" : game.DealerValue.ToString(), true)
            .AddField("Player Hand", game.PlayerValue.ToString(), true);

        builder.AddEmbed(embed);
        builder.AddFile("blackjack.png", new MemoryStream(imageBytes));

        if (game.Status == GameStatus.Playing)
        {
            builder.AddActionRowComponent(new DiscordActionRowComponent(new DiscordComponent[] {
                new DiscordButtonComponent(DiscordButtonStyle.Primary, $"bj_hit_{game.UserId}", "Hit"),
                new DiscordButtonComponent(DiscordButtonStyle.Secondary, $"bj_stand_{game.UserId}", "Stand")
            }));
        }
        else
        {
            builder.AddActionRowComponent(new DiscordActionRowComponent(new DiscordComponent[] {
                new DiscordButtonComponent(DiscordButtonStyle.Success, $"bj_play_again_{game.UserId}", "Play Again")
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
}
