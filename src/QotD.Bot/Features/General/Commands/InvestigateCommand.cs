using System.ComponentModel;
using DSharpPlus;
using DSharpPlus.Commands;
using DSharpPlus.Commands.Processors.SlashCommands;
using DSharpPlus.Entities;
using Microsoft.EntityFrameworkCore;
using QotD.Bot.Data;
using QotD.Bot.UI;

namespace QotD.Bot.Features.General.Commands;

/// <summary>
/// Command to investigate a user, using the Lawliet aesthetic.
/// </summary>
public sealed class InvestigateCommand
{
    private readonly AppDbContext _db;

    public InvestigateCommand(AppDbContext db)
    {
        _db = db;
    }

    [Command("investigate")]
    [Description("Startet eine Analyse des angegebenen Subjekts (Users).")]
    public async Task ExecuteAsync(
        CommandContext context,
        [Description("Das zu untersuchende Subjekt.")] DiscordUser user)
    {
        // Falls das Subjekt nicht im Server ist, DiscordMember abrufen falls möglich
        var member = user as DiscordMember;
        if (member is null && context.Guild is not null)
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

        var embed = CozyCoveUI.CreateBaseEmbed(
            "📁 Fallakte: Analyse läuft...",
            $"Die Untersuchung des Subjekts **{user.Username}** wurde eingeleitet.")
            .AddField("🆔 Subjekt-ID", user.Id.ToString(), true)
            .AddField("📅 Account erstellt", user.CreationTimestamp.ToString("d"), true);

        var subjectAvatarUrl = member?.AvatarUrl;
        if (string.IsNullOrWhiteSpace(subjectAvatarUrl))
        {
            subjectAvatarUrl = user.AvatarUrl;
        }

        if (!string.IsNullOrWhiteSpace(subjectAvatarUrl))
        {
            embed.WithThumbnail(subjectAvatarUrl);
        }

        if (member is not null)
        {
            embed.AddField("📥 Beigetreten", member.JoinedAt.ToString("d"), true)
                 .AddField("👤 Rollen", member.Roles.Any() ? string.Join(", ", member.Roles.Select(r => r.Name)) : "Keine", false);

            if (context.Guild is not null)
            {
                var activeWarnings = await _db.TeamWarnings
                    .AsNoTracking()
                    .Where(x => x.GuildId == context.Guild.Id && x.UserId == member.Id && x.IsActive)
                    .OrderByDescending(x => x.CreatedAtUtc)
                    .ToListAsync();

                var lastSnapshot = await _db.TeamActivityWeeklySnapshots
                    .AsNoTracking()
                    .Where(x => x.GuildId == context.Guild.Id && x.UserId == member.Id)
                    .OrderByDescending(x => x.WeekStartUtc)
                    .FirstOrDefaultAsync();

                embed.AddField("⚠️ Aktive Verwarnungen", activeWarnings.Count.ToString(), true);

                if (activeWarnings.Count > 0)
                {
                    var lastWarning = activeWarnings[0];
                    var source = lastWarning.CreatedByUserId == 0 ? "Auto" : "Manuell";
                    embed.AddField(
                        "🧾 Letzte Verwarnung",
                        $"{source} | {lastWarning.CreatedAtUtc:yyyy-MM-dd}\n{lastWarning.Reason}",
                        false);
                }

                if (lastSnapshot is not null)
                {
                    var status = lastSnapshot.WasExcused ? "Abgemeldet" : lastSnapshot.MeetsMinimum ? "OK" : "Unter Minimum";
                    embed.AddField(
                        "📊 Letzter Team-Activity-Snapshot",
                        $"Woche: {lastSnapshot.WeekStartUtc:yyyy-MM-dd}\n" +
                        $"Nachrichten: {lastSnapshot.Messages}\n" +
                        $"Voice-Minuten: {lastSnapshot.VoiceMinutes}\n" +
                        $"Score: {lastSnapshot.CombinedScore:F1}\n" +
                        $"Status: {status}",
                        false);
                }
            }
        }
        else
        {
            embed.AddField("📊 Status", "Subjekt befindet sich außerhalb des direkten Zugriffs.", false);
        }

        // TODO: Find correct property for latency in DSharpPlus v5 nightly
        embed.WithAnalyticalFooter(0);

        await context.RespondAsync(embed);
    }
}
