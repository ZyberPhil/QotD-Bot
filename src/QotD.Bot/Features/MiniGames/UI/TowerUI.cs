using System.Collections.Generic;
using System.Text;
using DSharpPlus;
using DSharpPlus.Entities;
using QotD.Bot.Features.MiniGames.Models;

namespace QotD.Bot.UI;

public static class TowerUI
{
    public static DiscordInteractionResponseBuilder BuildResponse(TowerGame game)
    {
        var builder = new DiscordInteractionResponseBuilder();

        var (towerText, statusText) = GenerateTowerDisplay(game);

        var embed = SectorUI.CreateBaseEmbed($"🏰 Tower Game | User: <@{game.UserId}>", towerText)
            .AddField("Aktueller Gewinn", $"{game.CurrentWin} Coins", true)
            .AddField("Status", statusText, true);

        builder.AddEmbed(embed);

        if (game.Status == TowerStatus.Playing)
        {
            var buttons = new List<DiscordComponent>();
            
            if (game.CurrentFloorIndex < game.Floors.Count)
            {
                var floor = game.Floors[game.CurrentFloorIndex];
                for (int i = 0; i < floor.TileCount; i++)
                {
                    buttons.Add(new DiscordButtonComponent(DiscordButtonStyle.Primary, $"tower_pick_{game.Id}_{i}", $"[{i + 1}]"));
                }
            }

            if (game.CurrentFloorIndex > 0)
            {
                buttons.Add(new DiscordButtonComponent(DiscordButtonStyle.Success, $"tower_cashout_{game.Id}", "Cash Out"));
            }

            if (buttons.Count > 0)
            {
                builder.AddActionRowComponent(new DiscordActionRowComponent(buttons));
            }
        }
        else
        {
            builder.AddActionRowComponent(new DiscordActionRowComponent(new DiscordComponent[] {
                new DiscordButtonComponent(DiscordButtonStyle.Primary, $"tower_play_again_{game.UserId}_{game.Bet}", "Nochmal Spielen")
            }));
        }

        return builder;
    }

    private static (string towerText, string statusText) GenerateTowerDisplay(TowerGame game)
    {
        var sb = new StringBuilder();

        // Build from top to bottom
        for (int i = game.Floors.Count - 1; i >= 0; i--)
        {
            var floor = game.Floors[i];
            var multiplier = game.Multipliers[i];
            
            if (i > game.CurrentFloorIndex)
            {
                // Unreached floor
                for(int t=0; t<floor.TileCount; t++) sb.Append("🟦 ");
                sb.Append($" *(Ebene {i + 1} - {multiplier:F2}x)*");
            }
            else if (i == game.CurrentFloorIndex)
            {
                // Current floor
                if (game.Status == TowerStatus.Playing)
                {
                    for(int t=0; t<floor.TileCount; t++) sb.Append("🟦 ");
                    sb.Append($" *(Ebene {i + 1} - {multiplier:F2}x)* <-- **Aktuell hier**");
                }
                else if (game.Status == TowerStatus.Lost)
                {
                    // Game Over on this floor
                    for(int t=0; t<floor.TileCount; t++)
                    {
                        if (t == floor.BombIndex) sb.Append(t == floor.SelectedTileIndex ? "💥 " : "💣 ");
                        else sb.Append(t == floor.SelectedTileIndex ? "✅ " : "🟩 ");
                    }
                    sb.Append($" *(Ebene {i + 1} - {multiplier:F2}x)*");
                }
                else
                {
                    // Cashed out or Won but stopped here
                    for(int t=0; t<floor.TileCount; t++)
                    {
                        if (t == floor.BombIndex) sb.Append("💣 ");
                        else sb.Append("🟩 ");
                    }
                    sb.Append($" *(Ebene {i + 1} - {multiplier:F2}x)*");
                }
            }
            else // i < game.CurrentFloorIndex -> passed floor
            {
                // Safe hits
                for(int t=0; t<floor.TileCount; t++)
                {
                    if (t == floor.SelectedTileIndex) sb.Append("✅ ");
                    else if (t == floor.BombIndex) sb.Append("💣 ");
                    else sb.Append("🟩 ");
                }
                sb.Append($" *(Ebene {i + 1} - {multiplier:F2}x)*");
            }

            sb.AppendLine();
        }

        string statusText = game.Status switch
        {
            TowerStatus.Playing => "Wähle das nächste Feld!",
            TowerStatus.CashedOut => $"Erfolgreich ausgezahlt! +{game.CurrentWin} Coins",
            TowerStatus.Lost => $"💥 Du hast eine Falle getroffen! Einsatz verloren.",
            TowerStatus.Won => $"🎉 Wahnsinn! Du hast die Spitze erreicht!",
            _ => ""
        };

        return (sb.ToString(), statusText);
    }
}
