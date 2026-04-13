using System.ComponentModel;
using DSharpPlus;
using DSharpPlus.Commands;
using DSharpPlus.Commands.ContextChecks;
using DSharpPlus.Entities;
using QotD.Bot.Data.Models;
using QotD.Bot.Features.Teams.Services;

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
            .WithColor(DiscordColor.Gold)
            .WithDescription(string.Join("\n", lines))
            .WithFooter("Score = Nachrichten + (Voice-Minuten / 10)")
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
            .WithColor(DiscordColor.SpringGreen)
            .WithDescription(
                $"Rolle: {role.Mention}\n" +
                $"Nachrichten/Woche: **{policy.MinMessagesPerWeek}**\n" +
                $"Voice-Minuten/Woche: **{policy.MinVoiceMinutesPerWeek}**")
            .WithTimestamp(DateTimeOffset.UtcNow);

        await ctx.RespondAsync(new DiscordMessageBuilder().AddEmbed(embed));
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
            .WithColor(DiscordColor.Orange)
            .WithDescription(string.Join("\n\n", lines))
            .WithTimestamp(DateTimeOffset.UtcNow);

        await ctx.RespondAsync(new DiscordMessageBuilder().AddEmbed(embed));
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
            .WithColor(DiscordColor.Blurple)
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
            .WithColor(DiscordColor.Azure)
            .WithDescription(string.Join("\n\n", lines))
            .WithTimestamp(DateTimeOffset.UtcNow);

        await ctx.RespondAsync(new DiscordMessageBuilder().AddEmbed(embed));
    }
}