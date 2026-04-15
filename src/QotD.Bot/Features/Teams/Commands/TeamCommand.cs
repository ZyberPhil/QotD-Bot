using System.ComponentModel;
using DSharpPlus;
using DSharpPlus.Commands;
using DSharpPlus.Commands.ContextChecks;
using DSharpPlus.Entities;
using QotD.Bot.Data.Models;
using QotD.Bot.Features.Teams.Services;
using QotD.Bot.UI;

namespace QotD.Bot.Features.Teams.Commands;

[Command("team")]
[Description("Team-Aktivität, Verwarnungen und Abmeldungen")]
public sealed class TeamCommand
{
    private readonly TeamActivityService _teamActivityService;

    public TeamCommand(TeamActivityService teamActivityService)
    {
        _teamActivityService = teamActivityService;
    }

    [Command("me")]
    [Description("Zeigt deinen aktuellen Wochenstatus, Rest bis zum Minimum und Verwarnungsstatus.")]
    public async ValueTask MeAsync(CommandContext ctx)
    {
        if (ctx.Guild is null)
        {
            await ctx.RespondAsync("Dieser Befehl kann nur in einem Server genutzt werden.");
            return;
        }

        var status = await _teamActivityService.BuildUserWeeklyStatusAsync(ctx.Guild, ctx.User.Id);
        if (status is null)
        {
            await ctx.RespondAsync("Du bist aktuell in keiner getrackten Team-Rolle.");
            return;
        }

        var roleLines = status.RoleProgress.Count == 0
            ? "Keine Team-Rolle mit Mindestwerten konfiguriert."
            : string.Join("\n", status.RoleProgress.Select(role =>
            {
                var roleName = ctx.Guild.Roles.GetValueOrDefault(role.RoleId)?.Mention ?? $"<@&{role.RoleId}>";
                var messageState = role.MeetsMessageMinimum ? "OK" : $"- {role.RemainingMessages} Msg";
                var voiceState = role.MeetsVoiceMinimum ? "OK" : $"- {role.RemainingVoiceMinutes} Min";
                return $"{roleName}: Msg {messageState}, Voice {voiceState}";
            }));

        var warningsText = status.ActiveWarningCount == 0
            ? "Keine aktiven Verwarnungen."
            : string.Join("\n", status.RecentWarnings.Select(w =>
            {
                var source = w.WarningType == TeamWarningType.MissingMinimum ? "Auto" : "Manuell";
                return $"#{w.WarningId} ({source}) - {w.Reason}";
            }));

        var embed = new DiscordEmbedBuilder()
            .WithTitle($"Dein Team-Status - {ctx.User.Username}")
            .WithColor(CozyCoveUI.CozyPrimary)
            .WithDescription(
                $"Nachrichten diese Woche: **{status.MessageCount}**\n" +
                $"Voice-Minuten diese Woche: **{status.VoiceMinutes}**\n" +
                $"Score: **{status.Score:F1}**")
            .AddField("Fortschritt", TruncateField(roleLines), false)
            .AddField("Warnungen", TruncateField(warningsText), false)
            .WithTimestamp(DateTimeOffset.UtcNow);

        await ctx.RespondAsync(new DiscordMessageBuilder().AddEmbed(embed));
    }

    [Command("ranking")]
    [Description("Zeigt das Team-Ranking für die aktuelle Woche.")]
    public async ValueTask RankingAsync(CommandContext ctx)
    {
        if (ctx.Guild is null)
        {
            await ctx.RespondAsync("Dieser Befehl kann nur in einem Server genutzt werden.");
            return;
        }

        var entries = await _teamActivityService.BuildRankingAsync(ctx.Guild);
        if (entries.Count == 0)
        {
            await ctx.RespondAsync("Keine Team-Mitglieder über die Team-Rollen gefunden. Richte zuerst /teamsetup ein.");
            return;
        }

        var lines = new List<string>(entries.Count);
        var rank = 1;
        foreach (var entry in entries)
        {
            var status = entry.IsExcused
                ? "Abgemeldet"
                : entry.MeetsMinimum ? "OK" : "Unter Minimum";

            lines.Add(
                $"**#{rank}** <@{entry.UserId}> | Nachrichten: **{entry.Messages}**/{entry.MinMessages} | Voice: **{entry.VoiceMinutes}**/{entry.MinVoiceMinutes} Min | Score: **{entry.Score:F1}** | {status}");
            rank += 1;
        }

        var embed = new DiscordEmbedBuilder()
            .WithTitle("Team Ranking (aktuelle Woche)")
            .WithColor(CozyCoveUI.CozyGold)
            .WithDescription(string.Join("\n", lines))
            .WithFeatureFooter("Teams", "Score = Nachrichten + (Voice-Minuten / 10)")
            .WithTimestamp(DateTimeOffset.UtcNow);

        await ctx.RespondAsync(new DiscordMessageBuilder().AddEmbed(embed));
    }

    [Command("minima")]
    [Description("Setzt Mindestaktivität pro Team-Rolle: Nachrichten und Voice-Minuten pro Woche.")]
    [RequirePermissions(DiscordPermission.ManageGuild)]
    public async ValueTask MinimaAsync(
        CommandContext ctx,
        [Description("Team-Rolle")] DiscordRole role,
        [Description("Mindestanzahl Nachrichten pro Woche")] long minMessages,
        [Description("Mindestanzahl Voice-Minuten pro Woche")] long minVoiceMinutes)
    {
        if (ctx.Guild is null)
        {
            await ctx.RespondAsync("Dieser Befehl kann nur in einem Server genutzt werden.");
            return;
        }

        var policy = await _teamActivityService.SetPolicyAsync(
            ctx.Guild.Id,
            role.Id,
            (int)Math.Max(0, minMessages),
            (int)Math.Max(0, minVoiceMinutes));

        var embed = new DiscordEmbedBuilder()
            .WithTitle("Mindestaktivität gesetzt")
            .WithColor(CozyCoveUI.CozySuccessGreen)
            .WithDescription(
                $"Rolle: {role.Mention}\n" +
                $"Nachrichten/Woche: **{policy.MinMessagesPerWeek}**\n" +
                $"Voice-Minuten/Woche: **{policy.MinVoiceMinutesPerWeek}**")
            .WithTimestamp(DateTimeOffset.UtcNow);

        await ctx.RespondAsync(new DiscordMessageBuilder().AddEmbed(embed));
    }

    [Command("reportsetup")]
    [Description("Legt den Kanal für den wöchentlichen Teamreport fest.")]
    [RequirePermissions(DiscordPermission.ManageGuild)]
    public async ValueTask ReportSetupAsync(CommandContext ctx, [Description("Kanal für den Wochenreport")] DiscordChannel channel)
    {
        if (ctx.Guild is null)
        {
            await ctx.RespondAsync("Dieser Befehl kann nur in einem Server genutzt werden.");
            return;
        }

        var config = await _teamActivityService.SetWeeklyReportChannelAsync(ctx.Guild.Id, channel.Id);
        await ctx.RespondAsync(new DiscordMessageBuilder().AddEmbed(new DiscordEmbedBuilder()
            .WithTitle("Wöchentlicher Teamreport aktiviert")
            .WithColor(CozyCoveUI.CozySuccessGreen)
            .WithDescription($"Berichte werden jetzt in {channel.Mention} gepostet.\nNächster Report läuft automatisch am Wochenwechsel.")
            .WithTimestamp(DateTimeOffset.UtcNow)));
    }

    [Command("reportdisable")]
    [Description("Deaktiviert den wöchentlichen Teamreport.")]
    [RequirePermissions(DiscordPermission.ManageGuild)]
    public async ValueTask ReportDisableAsync(CommandContext ctx)
    {
        if (ctx.Guild is null)
        {
            await ctx.RespondAsync("Dieser Befehl kann nur in einem Server genutzt werden.");
            return;
        }

        await _teamActivityService.DisableWeeklyReportAsync(ctx.Guild.Id);
        await ctx.RespondAsync("Der wöchentliche Teamreport wurde deaktiviert.");
    }

    [Command("warnings")]
    [Description("Zeigt aktive Team-Verwarnungen (optional für einen User).")]
    [RequirePermissions(DiscordPermission.ManageGuild)]
    public async ValueTask WarningsAsync(CommandContext ctx, [Description("Optionaler User")] DiscordUser? user = null)
    {
        if (ctx.Guild is null)
        {
            await ctx.RespondAsync("Dieser Befehl kann nur in einem Server genutzt werden.");
            return;
        }

        var warnings = await _teamActivityService.GetWarningsAsync(ctx.Guild.Id, user?.Id);
        if (warnings.Count == 0)
        {
            await ctx.RespondAsync("Keine aktiven Verwarnungen gefunden.");
            return;
        }

        var lines = warnings.Select(w =>
        {
            var source = w.WarningType == TeamWarningType.MissingMinimum ? "Auto" : "Manuell";
            var week = w.WeekStartUtc.HasValue ? $" | Woche: {w.WeekStartUtc.Value:yyyy-MM-dd}" : string.Empty;
            return $"**#{w.Id}** <@{w.UserId}> | {source}{week}\nGrund: {w.Reason}";
        });

        var embed = new DiscordEmbedBuilder()
            .WithTitle("Aktive Team-Verwarnungen")
            .WithColor(CozyCoveUI.CozyWarning)
            .WithDescription(string.Join("\n\n", lines))
            .WithTimestamp(DateTimeOffset.UtcNow);

        await ctx.RespondAsync(new DiscordMessageBuilder().AddEmbed(embed));
    }

    [Command("rolehistory")]
    [Description("Zeigt die Rollenwechsel-Historie eines Teammitglieds.")]
    public async ValueTask RoleHistoryAsync(CommandContext ctx, [Description("Optionaler User")] DiscordUser? user = null)
    {
        if (ctx.Guild is null)
        {
            await ctx.RespondAsync("Dieser Befehl kann nur in einem Server genutzt werden.");
            return;
        }

        var target = user ?? ctx.User;
        if (user is not null && ctx.Member?.Permissions.HasPermission(DiscordPermission.ManageGuild) != true)
        {
            await ctx.RespondAsync("Du kannst die Rollen-Historie anderer nur mit der Berechtigung Server verwalten einsehen.");
            return;
        }

        var history = await _teamActivityService.GetRoleChangeHistoryAsync(ctx.Guild.Id, target.Id);
        if (history.Count == 0)
        {
            await ctx.RespondAsync("Keine Rollenwechsel-Historie gefunden.");
            return;
        }

        var lines = history.Select(entry =>
        {
            var fromRole = entry.OldRoleName ?? (entry.OldRoleId.HasValue ? $"<@&{entry.OldRoleId.Value}>" : "Keine");
            var toRole = entry.NewRoleName ?? (entry.NewRoleId.HasValue ? $"<@&{entry.NewRoleId.Value}>" : "Keine");
            var reason = string.IsNullOrWhiteSpace(entry.ChangeReason) ? string.Empty : $" | {entry.ChangeReason}";
            return $"**{entry.ChangedAtUtc:yyyy-MM-dd HH:mm} UTC**: {fromRole} -> {toRole}{reason}";
        });

        await ctx.RespondAsync(new DiscordMessageBuilder().AddEmbed(new DiscordEmbedBuilder()
            .WithTitle($"Rollenwechsel von {target.Username}")
            .WithColor(CozyCoveUI.CozyPrimary)
            .WithDescription(string.Join("\n\n", lines))
            .WithTimestamp(DateTimeOffset.UtcNow)));
    }

    [Command("warningsadd")]
    [Description("Fügt eine manuelle Team-Verwarnung hinzu.")]
    [RequirePermissions(DiscordPermission.ManageGuild)]
    public async ValueTask WarningsAddAsync(
        CommandContext ctx,
        [Description("User")] DiscordUser user,
        [Description("Grund")] string reason)
    {
        if (ctx.Guild is null)
        {
            await ctx.RespondAsync("Dieser Befehl kann nur in einem Server genutzt werden.");
            return;
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            await ctx.RespondAsync("Bitte gib einen Grund an.");
            return;
        }

        var warning = await _teamActivityService.AddManualWarningAsync(ctx.Guild.Id, user.Id, ctx.User.Id, reason);
        await ctx.RespondAsync($"Verwarnung **#{warning.Id}** für {user.Mention} wurde erstellt.");
    }

    [Command("warningsremove")]
    [Description("Entfernt eine Verwarnung per ID (setzt sie auf inaktiv).")]
    [RequirePermissions(DiscordPermission.ManageGuild)]
    public async ValueTask WarningsRemoveAsync(
        CommandContext ctx,
        [Description("Warnungs-ID")] long warningId)
    {
        if (ctx.Guild is null)
        {
            await ctx.RespondAsync("Dieser Befehl kann nur in einem Server genutzt werden.");
            return;
        }

        var removed = await _teamActivityService.RemoveWarningAsync(ctx.Guild.Id, (int)warningId);
        await ctx.RespondAsync(removed
            ? "Verwarnung wurde entfernt."
            : "Warnung nicht gefunden oder bereits entfernt.");
    }

    [Command("leavestart")]
    [Description("Meldet dich vom Team ab (z.B. Urlaub) mit Grund und optionaler Dauer in Tagen.")]
    public async ValueTask LeaveStartAsync(
        CommandContext ctx,
        [Description("Grund für die Abmeldung")] string reason,
        [Description("Optionale Dauer in Tagen")] long? days = null)
    {
        if (ctx.Guild is null)
        {
            await ctx.RespondAsync("Dieser Befehl kann nur in einem Server genutzt werden.");
            return;
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            await ctx.RespondAsync("Bitte gib einen Grund für die Abmeldung an.");
            return;
        }

        DateTimeOffset? endUtc = null;
        if (days.HasValue && days.Value > 0)
        {
            endUtc = DateTimeOffset.UtcNow.AddDays(days.Value);
        }

        try
        {
            var leave = await _teamActivityService.StartLeaveAsync(ctx.Guild.Id, ctx.User.Id, reason, endUtc);
            var untilText = leave.EndUtc.HasValue ? $" bis **{leave.EndUtc.Value:yyyy-MM-dd HH:mm} UTC**" : " (offen, bis du sie beendest)";
            await ctx.RespondAsync($"Abmeldung gestartet{untilText}. Grund: **{leave.Reason}**");
        }
        catch (InvalidOperationException ex)
        {
            await ctx.RespondAsync(ex.Message);
        }
    }

    [Command("leaveend")]
    [Description("Beendet deine aktive Abmeldung.")]
    public async ValueTask LeaveEndAsync(CommandContext ctx)
    {
        if (ctx.Guild is null)
        {
            await ctx.RespondAsync("Dieser Befehl kann nur in einem Server genutzt werden.");
            return;
        }

        var ended = await _teamActivityService.EndLeaveAsync(ctx.Guild.Id, ctx.User.Id);
        await ctx.RespondAsync(ended
            ? "Deine Abmeldung wurde beendet."
            : "Du hast aktuell keine aktive Abmeldung.");
    }

    [Command("leavestats")]
    [Description("Zeigt Anzahl und Gesamtdauer aller Abmeldungen eines Users (oder von dir selbst).")]
    public async ValueTask LeaveStatsAsync(CommandContext ctx, [Description("Optionaler User")] DiscordUser? user = null)
    {
        if (ctx.Guild is null)
        {
            await ctx.RespondAsync("Dieser Befehl kann nur in einem Server genutzt werden.");
            return;
        }

        var target = user ?? ctx.User;
        var stats = await _teamActivityService.GetLeaveStatsAsync(ctx.Guild.Id, target.Id);

        var embed = new DiscordEmbedBuilder()
            .WithTitle("Abmeldungs-Statistik")
            .WithColor(CozyCoveUI.CozyPrimary)
            .WithDescription(
                $"User: {target.Mention}\n" +
                $"Anzahl Abmeldungen: **{stats.Count}**\n" +
                $"Gesamtdauer: **{(int)stats.TotalDuration.TotalDays} Tage {stats.TotalDuration.Hours} Stunden**")
            .WithTimestamp(DateTimeOffset.UtcNow);

        await ctx.RespondAsync(new DiscordMessageBuilder().AddEmbed(embed));
    }

    [Command("leavehistory")]
    [Description("Zeigt Abmeldungen eines Users mit Grund und Zeitraum.")]
    public async ValueTask LeaveHistoryAsync(CommandContext ctx, [Description("Optionaler User")] DiscordUser? user = null)
    {
        if (ctx.Guild is null)
        {
            await ctx.RespondAsync("Dieser Befehl kann nur in einem Server genutzt werden.");
            return;
        }

        var target = user ?? ctx.User;
        if (user is not null && ctx.Member?.Permissions.HasPermission(DiscordPermission.ManageGuild) != true)
        {
            await ctx.RespondAsync("Du kannst fremde Abmeldungen nur mit der Berechtigung Server verwalten einsehen.");
            return;
        }

        var entries = await _teamActivityService.GetLeaveHistoryAsync(ctx.Guild.Id, target.Id);
        if (entries.Count == 0)
        {
            await ctx.RespondAsync("Keine Abmeldungen gefunden.");
            return;
        }

        var lines = entries.Select(entry =>
        {
            var endText = entry.EndUtc.HasValue ? entry.EndUtc.Value.ToString("yyyy-MM-dd HH:mm") + " UTC" : "offen";
            return $"**{entry.StartUtc:yyyy-MM-dd HH:mm} UTC** bis **{endText}**\nGrund: {entry.Reason}";
        });

        var embed = new DiscordEmbedBuilder()
            .WithTitle($"Abmeldungen von {target.Username}")
            .WithColor(CozyCoveUI.CozyInfoBlue)
            .WithDescription(string.Join("\n\n", lines))
            .WithTimestamp(DateTimeOffset.UtcNow);

        await ctx.RespondAsync(new DiscordMessageBuilder().AddEmbed(embed));
    }

    [Command("warningsnote")]
    [Description("Notizen und Abschlussstatus zu einer Warnung verwalten.")]
    public sealed class WarningNoteGroup
    {
        private readonly TeamActivityService _teamActivityService;

        public WarningNoteGroup(TeamActivityService teamActivityService)
        {
            _teamActivityService = teamActivityService;
        }

        [Command("lead")]
        [Description("Fügt einer Warnung einen Kommentar vom Teamlead hinzu.")]
        [RequirePermissions(DiscordPermission.ManageGuild)]
        public async ValueTask LeadCommentAsync(CommandContext ctx, [Description("Warnungs-ID")] long warningId, [Description("Kommentar")] string content)
        {
            await AddNoteAsync(ctx, (int)warningId, TeamWarningNoteType.LeadComment, content, requireOwner: false);
        }

        [Command("statement")]
        [Description("Fügt einer Warnung die Stellungnahme des Users hinzu.")]
        public async ValueTask UserStatementAsync(CommandContext ctx, [Description("Warnungs-ID")] long warningId, [Description("Stellungnahme")] string content)
        {
            await AddNoteAsync(ctx, (int)warningId, TeamWarningNoteType.UserStatement, content, requireOwner: true);
        }

        [Command("resolve")]
        [Description("Schließt eine Warnung mit einer Abschlussnotiz.")]
        [RequirePermissions(DiscordPermission.ManageGuild)]
        public async ValueTask ResolveAsync(CommandContext ctx, [Description("Warnungs-ID")] long warningId, [Description("Abschlussnotiz")] string content)
        {
            await AddNoteAsync(ctx, (int)warningId, TeamWarningNoteType.ResolutionNote, content, requireOwner: false);
        }

        [Command("list")]
        [Description("Zeigt alle Notizen zu einer Warnung an.")]
        public async ValueTask ListAsync(CommandContext ctx, [Description("Warnungs-ID")] long warningId)
        {
            if (ctx.Guild is null)
            {
                await ctx.RespondAsync("Dieser Befehl kann nur in einem Server genutzt werden.");
                return;
            }

            var warning = await _teamActivityService.GetWarningByIdAsync(ctx.Guild.Id, (int)warningId);
            if (warning is null)
            {
                await ctx.RespondAsync("Warnung nicht gefunden.");
                return;
            }

            if (ctx.Member?.Permissions.HasPermission(DiscordPermission.ManageGuild) != true && warning.UserId != ctx.User.Id)
            {
                await ctx.RespondAsync("Du kannst nur deine eigenen Warnungen einsehen.");
                return;
            }

            var notes = await _teamActivityService.GetWarningNotesAsync(ctx.Guild.Id, (int)warningId);
            if (notes.Count == 0)
            {
                await ctx.RespondAsync("Keine Notizen zu dieser Warnung vorhanden.");
                return;
            }

            var lines = notes.Select(note =>
            {
                var kind = note.NoteType switch
                {
                    TeamWarningNoteType.LeadComment => "Teamlead",
                    TeamWarningNoteType.UserStatement => "User",
                    _ => "Abschluss"
                };

                return $"**{note.CreatedAtUtc:yyyy-MM-dd HH:mm} UTC** [{kind}] <@{note.AuthorUserId}>\n{note.Content}";
            });

            await ctx.RespondAsync(new DiscordMessageBuilder().AddEmbed(new DiscordEmbedBuilder()
                .WithTitle($"Notizen zu Warnung #{warningId}")
                .WithColor(CozyCoveUI.CozyWarning)
                .WithDescription(string.Join("\n\n", lines))
                .WithFeatureFooter("Teams", warning.IsResolved ? "Status: gelöst" : "Status: offen")
                .WithTimestamp(DateTimeOffset.UtcNow)));
        }

        private async Task AddNoteAsync(CommandContext ctx, int warningId, TeamWarningNoteType type, string content, bool requireOwner)
        {
            if (ctx.Guild is null)
            {
                await ctx.RespondAsync("Dieser Befehl kann nur in einem Server genutzt werden.");
                return;
            }

            if (string.IsNullOrWhiteSpace(content))
            {
                await ctx.RespondAsync("Bitte gib einen Inhalt an.");
                return;
            }

            var warning = await _teamActivityService.GetWarningByIdAsync(ctx.Guild.Id, warningId);
            if (warning is null)
            {
                await ctx.RespondAsync("Warnung nicht gefunden.");
                return;
            }

            if (requireOwner && ctx.Member?.Permissions.HasPermission(DiscordPermission.ManageGuild) != true && warning.UserId != ctx.User.Id)
            {
                await ctx.RespondAsync("Du kannst nur auf deine eigenen Warnungen antworten.");
                return;
            }

            var note = await _teamActivityService.AddWarningNoteAsync(ctx.Guild.Id, warningId, ctx.User.Id, type, content);
            var response = type == TeamWarningNoteType.ResolutionNote
                ? "Warnung wurde geschlossen und mit einer Abschlussnotiz versehen."
                : "Notiz wurde gespeichert.";

            await ctx.RespondAsync($"{response} (#{note.Id})");
        }
    }

    private static string TruncateField(string content)
    {
        return content.Length <= 1024 ? content : content[..1021] + "...";
    }
}