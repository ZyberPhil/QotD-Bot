using System.ComponentModel;
using DSharpPlus.Commands;
using DSharpPlus.Commands.Processors.SlashCommands;
using DSharpPlus.Entities;
using QotD.Bot.UI;

namespace QotD.Bot.Commands;

/// <summary>
/// Command to investigate a user, using the Lawliet aesthetic.
/// </summary>
public sealed class InvestigateCommand
{
    [Command("investigate")]
    [Description("Startet eine Analyse des angegebenen Subjekts (Users).")]
    public async Task ExecuteAsync(
        CommandContext context,
        [Description("Das zu untersuchende Subjekt.")] DiscordUser user)
    {
        // Falls das Subjekt nicht im Server ist, DiscordMember abrufen falls möglich
        var member = user as DiscordMember;
        if (member == null && context.Guild != null)
        {
            try
            {
                member = await context.Guild.GetMemberAsync(user.Id);
            }
            catch
            {
                // Subjekt ist nicht auf diesem Server präsent
            }
        }

        var embed = LawlietUI.CreateBaseEmbed(
            "📁 Fallakte: Analyse läuft...",
            $"Die Untersuchung des Subjekts **{user.Username}** wurde eingeleitet.")
            .AddField("🆔 Subjekt-ID", user.Id.ToString(), true)
            .AddField("📅 Account erstellt", user.CreationTimestamp.ToString("d"), true);

        if (member != null)
        {
            embed.AddField("📥 Beigetreten", member.JoinedAt.ToString("d"), true)
                 .AddField("👤 Rollen", member.Roles.Any() ? string.Join(", ", member.Roles.Select(r => r.Name)) : "Keine", false);
        }
        else
        {
            embed.AddField("📊 Status", "Subjekt befindet sich außerhalb des direkten Zugriffs.", false);
        }

        embed.WithAnalyticalFooter((int)context.Client.Ping);

        await context.RespondAsync(embed);
    }
}
