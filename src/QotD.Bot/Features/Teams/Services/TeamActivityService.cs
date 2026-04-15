using DSharpPlus;
using DSharpPlus.Entities;
using Microsoft.EntityFrameworkCore;
using QotD.Bot.Data;
using QotD.Bot.Data.Models;
using QotD.Bot.Features.Leveling.Services;
using QotD.Bot.UI;
using System.Text;

namespace QotD.Bot.Features.Teams.Services;

public sealed class TeamActivityService(
    AppDbContext db,
    LevelService levelService)
{
    private const double VoiceWeight = 0.1d;

    public async Task<TeamActivityPolicy> SetPolicyAsync(
        ulong guildId,
        ulong roleId,
        int minMessagesPerWeek,
        int minVoiceMinutesPerWeek)
    {
        var normalizedMessages = Math.Max(0, minMessagesPerWeek);
        var normalizedVoiceMinutes = Math.Max(0, minVoiceMinutesPerWeek);

        var policy = await db.TeamActivityPolicies
            .FirstOrDefaultAsync(x => x.GuildId == guildId && x.RoleId == roleId);

        if (policy is null)
        {
            policy = new TeamActivityPolicy
            {
                GuildId = guildId,
                RoleId = roleId,
                MinMessagesPerWeek = normalizedMessages,
                MinVoiceMinutesPerWeek = normalizedVoiceMinutes,
                IsEnabled = true,
                CreatedAtUtc = DateTimeOffset.UtcNow
            };

            db.TeamActivityPolicies.Add(policy);
        }
        else
        {
            policy.MinMessagesPerWeek = normalizedMessages;
            policy.MinVoiceMinutesPerWeek = normalizedVoiceMinutes;
            policy.IsEnabled = true;
        }

        await db.SaveChangesAsync();
        return policy;
    }

    public async Task<IReadOnlyList<TeamActivityPolicy>> GetPoliciesAsync(ulong guildId)
    {
        return await db.TeamActivityPolicies
            .AsNoTracking()
            .Where(x => x.GuildId == guildId)
            .OrderByDescending(x => x.MinMessagesPerWeek + x.MinVoiceMinutesPerWeek)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<TeamRankingEntry>> BuildRankingAsync(DiscordGuild guild)
    {
        var weekStartUtc = GetWeekStartUtc(DateTimeOffset.UtcNow);
        var nowUtc = DateTimeOffset.UtcNow;

        var context = await BuildTeamContextAsync(guild);
        if (context.Members.Count == 0)
        {
            return [];
        }

        var userIds = context.Members.Select(x => x.UserId).ToArray();
        var activity = await levelService.GetUserActivitySummaryAsync(guild.Id, userIds, weekStartUtc, nowUtc);
        var leaves = await GetOverlappingLeavesAsync(guild.Id, weekStartUtc, nowUtc);

        var entries = new List<TeamRankingEntry>(context.Members.Count);
        foreach (var member in context.Members)
        {
            var policy = context.PolicyByRoleId.GetValueOrDefault(member.PrimaryRoleId);
            var summary = activity.GetValueOrDefault(member.UserId, new LevelUserActivitySummary(0, 0));
            var isExcused = leaves.Contains(member.UserId);

            var meetsMinimum = isExcused || MeetsMinimum(summary, policy);
            var score = CalculateScore(summary.MessageCount, summary.VoiceMinutes);

            entries.Add(new TeamRankingEntry(
                member.UserId,
                member.PrimaryRoleId,
                summary.MessageCount,
                summary.VoiceMinutes,
                score,
                policy?.MinMessagesPerWeek ?? 0,
                policy?.MinVoiceMinutesPerWeek ?? 0,
                meetsMinimum,
                isExcused));
        }

        return entries
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Messages)
            .ThenByDescending(x => x.VoiceMinutes)
            .ToList();
    }

    public async Task EvaluatePreviousWeekAndWarnAsync(DiscordClient client, ulong guildId, CancellationToken ct)
    {
        if (!client.Guilds.TryGetValue(guildId, out var guild))
        {
            return;
        }

        var currentWeek = GetWeekStartUtc(DateTimeOffset.UtcNow);
        var previousWeek = currentWeek.AddDays(-7);

        var alreadyProcessed = await db.TeamActivityWeeklySnapshots
            .AsNoTracking()
            .AnyAsync(x => x.GuildId == guildId && x.WeekStartUtc == previousWeek, ct);
        if (alreadyProcessed)
        {
            return;
        }

        var context = await BuildTeamContextAsync(guild);
        if (context.Members.Count == 0)
        {
            return;
        }

        var previousWeekEnd = previousWeek.AddDays(7);
        var userIds = context.Members.Select(x => x.UserId).ToArray();
        var activity = await levelService.GetUserActivitySummaryAsync(guildId, userIds, previousWeek, previousWeekEnd);
        var leaves = await GetOverlappingLeavesAsync(guildId, previousWeek, previousWeekEnd);

        foreach (var member in context.Members)
        {
            if (ct.IsCancellationRequested)
            {
                return;
            }

            var policy = context.PolicyByRoleId.GetValueOrDefault(member.PrimaryRoleId);
            var summary = activity.GetValueOrDefault(member.UserId, new LevelUserActivitySummary(0, 0));
            var isExcused = leaves.Contains(member.UserId);
            var meetsMinimum = isExcused || MeetsMinimum(summary, policy);
            var score = CalculateScore(summary.MessageCount, summary.VoiceMinutes);

            db.TeamActivityWeeklySnapshots.Add(new TeamActivityWeeklySnapshot
            {
                GuildId = guildId,
                UserId = member.UserId,
                RoleId = member.PrimaryRoleId,
                WeekStartUtc = previousWeek,
                Messages = summary.MessageCount,
                VoiceMinutes = summary.VoiceMinutes,
                CombinedScore = score,
                MeetsMinimum = meetsMinimum,
                WasExcused = isExcused,
                CreatedAtUtc = DateTimeOffset.UtcNow
            });

            if (meetsMinimum)
            {
                continue;
            }

            var exists = await db.TeamWarnings
                .AnyAsync(x =>
                    x.GuildId == guildId &&
                    x.UserId == member.UserId &&
                    x.RoleId == member.PrimaryRoleId &&
                    x.WarningType == TeamWarningType.MissingMinimum &&
                    x.WeekStartUtc == previousWeek,
                    ct);

            if (exists)
            {
                continue;
            }

            var reason = BuildMissingMinimumReason(policy, summary.MessageCount, summary.VoiceMinutes);
            db.TeamWarnings.Add(new TeamWarning
            {
                GuildId = guildId,
                UserId = member.UserId,
                RoleId = member.PrimaryRoleId,
                WarningType = TeamWarningType.MissingMinimum,
                Reason = reason,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedByUserId = 0,
                IsActive = true,
                WeekStartUtc = previousWeek
            });

            await TrySendMinimumWarningDmAsync(client, member.UserId, guild, previousWeek, reason);
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task<TeamWarning> AddManualWarningAsync(ulong guildId, ulong userId, ulong moderatorId, string reason)
    {
        var warning = new TeamWarning
        {
            GuildId = guildId,
            UserId = userId,
            WarningType = TeamWarningType.Manual,
            Reason = reason.Trim(),
            CreatedAtUtc = DateTimeOffset.UtcNow,
            CreatedByUserId = moderatorId,
            IsActive = true
        };

        db.TeamWarnings.Add(warning);
        await db.SaveChangesAsync();
        return warning;
    }

    public async Task<bool> RemoveWarningAsync(ulong guildId, int warningId)
    {
        var warning = await db.TeamWarnings
            .FirstOrDefaultAsync(x => x.Id == warningId && x.GuildId == guildId && x.IsActive);

        if (warning is null)
        {
            return false;
        }

        warning.IsActive = false;
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<IReadOnlyList<TeamWarning>> GetWarningsAsync(ulong guildId, ulong? userId = null)
    {
        var query = db.TeamWarnings
            .AsNoTracking()
            .Where(x => x.GuildId == guildId && x.IsActive && !x.IsResolved);

        if (userId.HasValue)
        {
            query = query.Where(x => x.UserId == userId.Value);
        }

        return await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(50)
            .ToListAsync();
    }

    public async Task<TeamWarning?> GetWarningByIdAsync(ulong guildId, int warningId)
    {
        return await db.TeamWarnings
            .FirstOrDefaultAsync(x => x.GuildId == guildId && x.Id == warningId);
    }

    public async Task<TeamLeaveEntry> StartLeaveAsync(ulong guildId, ulong userId, string reason, DateTimeOffset? endUtc)
    {
        var nowUtc = DateTimeOffset.UtcNow;
        var openLeave = await db.TeamLeaveEntries
            .FirstOrDefaultAsync(x => x.GuildId == guildId && x.UserId == userId && x.EndUtc == null);

        if (openLeave is not null)
        {
            throw new InvalidOperationException("Du hast bereits eine aktive Abmeldung.");
        }

        var leave = new TeamLeaveEntry
        {
            GuildId = guildId,
            UserId = userId,
            Reason = reason.Trim(),
            StartUtc = nowUtc,
            EndUtc = endUtc,
            CreatedAtUtc = nowUtc
        };

        db.TeamLeaveEntries.Add(leave);
        await db.SaveChangesAsync();
        return leave;
    }

    public async Task<bool> EndLeaveAsync(ulong guildId, ulong userId)
    {
        var leave = await db.TeamLeaveEntries
            .Where(x => x.GuildId == guildId && x.UserId == userId && x.EndUtc == null)
            .OrderByDescending(x => x.StartUtc)
            .FirstOrDefaultAsync();

        if (leave is null)
        {
            return false;
        }

        leave.EndUtc = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<TeamLeaveStats> GetLeaveStatsAsync(ulong guildId, ulong userId)
    {
        var entries = await db.TeamLeaveEntries
            .AsNoTracking()
            .Where(x => x.GuildId == guildId && x.UserId == userId)
            .ToListAsync();

        var totalDuration = TimeSpan.Zero;
        foreach (var entry in entries)
        {
            var end = entry.EndUtc ?? DateTimeOffset.UtcNow;
            if (end > entry.StartUtc)
            {
                totalDuration += end - entry.StartUtc;
            }
        }

        return new TeamLeaveStats(entries.Count, totalDuration);
    }

    public async Task<IReadOnlyList<TeamLeaveEntry>> GetLeaveHistoryAsync(ulong guildId, ulong userId)
    {
        return await db.TeamLeaveEntries
            .AsNoTracking()
            .Where(x => x.GuildId == guildId && x.UserId == userId)
            .OrderByDescending(x => x.StartUtc)
            .Take(30)
            .ToListAsync();
    }

    public async Task<TeamWeeklyReportConfig> SetWeeklyReportChannelAsync(ulong guildId, ulong channelId)
    {
        var config = await db.TeamWeeklyReportConfigs
            .FirstOrDefaultAsync(x => x.GuildId == guildId);

        var currentWeekStart = GetWeekStartUtc(DateTimeOffset.UtcNow);

        if (config is null)
        {
            config = new TeamWeeklyReportConfig
            {
                GuildId = guildId,
                ChannelId = channelId,
                IsEnabled = true,
                LastReportedWeekStartUtc = currentWeekStart,
                CreatedAtUtc = DateTimeOffset.UtcNow
            };

            db.TeamWeeklyReportConfigs.Add(config);
        }
        else
        {
            config.ChannelId = channelId;
            config.IsEnabled = true;
            config.LastReportedWeekStartUtc = currentWeekStart;
        }

        await db.SaveChangesAsync();
        return config;
    }

    public async Task DisableWeeklyReportAsync(ulong guildId)
    {
        var config = await db.TeamWeeklyReportConfigs
            .FirstOrDefaultAsync(x => x.GuildId == guildId);

        if (config is null)
        {
            return;
        }

        config.IsEnabled = false;
        await db.SaveChangesAsync();
    }

    public async Task<TeamWeeklyReportConfig?> GetWeeklyReportConfigAsync(ulong guildId)
    {
        return await db.TeamWeeklyReportConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.GuildId == guildId);
    }

    public async Task<TeamUserWeeklyStatus?> BuildUserWeeklyStatusAsync(DiscordGuild guild, ulong userId)
    {
        var context = await BuildTeamContextAsync(guild);
        var member = context.Members.FirstOrDefault(x => x.UserId == userId);
        if (member is null)
        {
            return null;
        }

        var weekStartUtc = GetWeekStartUtc(DateTimeOffset.UtcNow);
        var nowUtc = DateTimeOffset.UtcNow;
        var activity = await levelService.GetUserActivitySummaryAsync(guild.Id, [userId], weekStartUtc, nowUtc);
        var summary = activity.GetValueOrDefault(userId, new LevelUserActivitySummary(0, 0));
        var activeWarnings = await db.TeamWarnings
            .AsNoTracking()
            .Where(x => x.GuildId == guild.Id && x.UserId == userId && x.IsActive && !x.IsResolved)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync();

        var roles = context.Members
            .Where(x => x.UserId == userId)
            .Select(x => x.PrimaryRoleId)
            .Distinct()
            .ToArray();

        var roleProgress = new List<TeamRoleProgress>();
        foreach (var roleId in roles)
        {
            var policy = context.PolicyByRoleId.GetValueOrDefault(roleId);
            if (policy is null)
            {
                continue;
            }

            roleProgress.Add(new TeamRoleProgress(
                roleId,
                policy.MinMessagesPerWeek,
                policy.MinVoiceMinutesPerWeek,
                Math.Max(0, policy.MinMessagesPerWeek - summary.MessageCount),
                Math.Max(0, policy.MinVoiceMinutesPerWeek - summary.VoiceMinutes),
                summary.MessageCount >= policy.MinMessagesPerWeek,
                summary.VoiceMinutes >= policy.MinVoiceMinutesPerWeek));
        }

        return new TeamUserWeeklyStatus(
            userId,
            summary.MessageCount,
            summary.VoiceMinutes,
            CalculateScore(summary.MessageCount, summary.VoiceMinutes),
            activeWarnings.Count,
            activeWarnings.Take(3).Select(x => new TeamWarningSummary(x.Id, x.Reason, x.WarningType, x.CreatedAtUtc, x.IsResolved)).ToList(),
            roleProgress);
    }

    public async Task<IReadOnlyList<TeamRoleChangeHistory>> GetRoleChangeHistoryAsync(ulong guildId, ulong userId)
    {
        return await db.TeamRoleChangeHistories
            .AsNoTracking()
            .Where(x => x.GuildId == guildId && x.UserId == userId)
            .OrderByDescending(x => x.ChangedAtUtc)
            .Take(30)
            .ToListAsync();
    }

    public async Task RecordRoleChangeAsync(
        ulong guildId,
        ulong userId,
        ulong? oldRoleId,
        string? oldRoleName,
        ulong? newRoleId,
        string? newRoleName,
        DateTimeOffset changedAtUtc,
        string? changeReason = null)
    {
        if (oldRoleId == newRoleId)
        {
            return;
        }

        db.TeamRoleChangeHistories.Add(new TeamRoleChangeHistory
        {
            GuildId = guildId,
            UserId = userId,
            OldRoleId = oldRoleId,
            OldRoleName = oldRoleName,
            NewRoleId = newRoleId,
            NewRoleName = newRoleName,
            ChangedAtUtc = changedAtUtc,
            ChangeReason = changeReason
        });

        await db.SaveChangesAsync();
    }

    public async Task<TeamWarningNote> AddWarningNoteAsync(
        ulong guildId,
        int warningId,
        ulong authorUserId,
        TeamWarningNoteType noteType,
        string content)
    {
        var warning = await db.TeamWarnings
            .FirstOrDefaultAsync(x => x.Id == warningId && x.GuildId == guildId);

        if (warning is null)
        {
            throw new InvalidOperationException("Warnung nicht gefunden.");
        }

        var note = new TeamWarningNote
        {
            WarningId = warningId,
            GuildId = guildId,
            AuthorUserId = authorUserId,
            NoteType = noteType,
            Content = content.Trim(),
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        db.TeamWarningNotes.Add(note);

        if (noteType == TeamWarningNoteType.ResolutionNote)
        {
            warning.IsResolved = true;
            warning.IsActive = false;
            warning.ResolvedAtUtc = note.CreatedAtUtc;
            warning.ResolvedByUserId = authorUserId;
            warning.ResolutionNote = note.Content;
        }

        await db.SaveChangesAsync();
        return note;
    }

    public async Task<IReadOnlyList<TeamWarningNote>> GetWarningNotesAsync(ulong guildId, int warningId)
    {
        return await db.TeamWarningNotes
            .AsNoTracking()
            .Where(x => x.GuildId == guildId && x.WarningId == warningId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync();
    }

    public async Task<TeamWeeklyReportResult?> TryBuildWeeklyReportAsync(ulong guildId, DateTimeOffset weekStartUtc)
    {
        var guildContext = await db.TeamListConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.GuildId == guildId);

        if (guildContext is null || guildContext.TrackedRoles.Length == 0)
        {
            return null;
        }

        var trackedRoleIds = guildContext.TrackedRoles;
        var weekEndUtc = weekStartUtc.AddDays(7);

        var snapshots = await db.TeamActivityWeeklySnapshots
            .AsNoTracking()
            .Where(x => x.GuildId == guildId && x.WeekStartUtc == weekStartUtc)
            .ToListAsync();

        var topActive = snapshots
            .OrderByDescending(x => x.CombinedScore)
            .ThenByDescending(x => x.Messages)
            .ThenByDescending(x => x.VoiceMinutes)
            .Take(5)
            .ToList();

        var underMinimum = snapshots
            .Where(x => !x.MeetsMinimum && !x.WasExcused)
            .OrderByDescending(x => x.Messages + x.VoiceMinutes)
            .ToList();

        var leaveEntries = await db.TeamLeaveEntries
            .AsNoTracking()
            .Where(x => x.GuildId == guildId)
            .Where(x => x.StartUtc < weekEndUtc)
            .Where(x => x.EndUtc == null || x.EndUtc > weekStartUtc)
            .ToListAsync();

        var warnings = await db.TeamWarnings
            .AsNoTracking()
            .Where(x => x.GuildId == guildId && x.CreatedAtUtc >= weekStartUtc && x.CreatedAtUtc < weekEndUtc)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync();

        return new TeamWeeklyReportResult(
            guildId,
            weekStartUtc,
            weekEndUtc,
            topActive,
            underMinimum,
            leaveEntries,
            warnings,
            trackedRoleIds);
    }

    public async Task ProcessWeeklyReportsAsync(DiscordClient client, CancellationToken ct)
    {
        var currentWeekStart = GetWeekStartUtc(DateTimeOffset.UtcNow);

        var configs = await db.TeamWeeklyReportConfigs
            .Where(x => x.IsEnabled && x.ChannelId.HasValue)
            .ToListAsync(ct);

        foreach (var config in configs)
        {
            if (ct.IsCancellationRequested)
            {
                return;
            }

            var lastReportedWeek = config.LastReportedWeekStartUtc;
            if (lastReportedWeek.HasValue && lastReportedWeek.Value >= currentWeekStart)
            {
                continue;
            }

            var reportWeekStart = currentWeekStart.AddDays(-7);
            var report = await TryBuildWeeklyReportAsync(config.GuildId, reportWeekStart);
            if (report is null)
            {
                continue;
            }

            if (!client.Guilds.TryGetValue(config.GuildId, out var guild))
            {
                continue;
            }

            if (!guild.Channels.TryGetValue(config.ChannelId!.Value, out var channel))
            {
                continue;
            }

            var embed = BuildWeeklyReportEmbed(guild, report);
            await channel.SendMessageAsync(new DiscordMessageBuilder().AddEmbed(embed));

            config.LastReportedWeekStartUtc = currentWeekStart;
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task<TeamContext> BuildTeamContextAsync(DiscordGuild guild)
    {
        var config = await db.TeamListConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.GuildId == guild.Id);

        if (config is null || config.TrackedRoles.Length == 0)
        {
            return new TeamContext([], new Dictionary<ulong, TeamActivityPolicy>());
        }

        var trackedRoles = config.TrackedRoles
            .Select(id => guild.Roles.GetValueOrDefault(id))
            .Where(x => x is not null)
            .OrderByDescending(x => x!.Position)
            .ToArray();

        var members = new Dictionary<ulong, TeamContextMember>();
        await foreach (var member in guild.GetAllMembersAsync())
        {
            if (member.IsBot)
            {
                continue;
            }

            var matchingRoles = trackedRoles
                .Where(role => member.Roles.Any(r => r.Id == role!.Id))
                .Select(role => role!.Id)
                .ToArray();

            if (matchingRoles.Length == 0)
            {
                continue;
            }

            var primaryRoleId = matchingRoles[0];
            members[member.Id] = new TeamContextMember(member.Id, primaryRoleId);
        }

        var roleIds = trackedRoles.Select(x => x!.Id).ToArray();
        var policies = await db.TeamActivityPolicies
            .AsNoTracking()
            .Where(x => x.GuildId == guild.Id && roleIds.Contains(x.RoleId) && x.IsEnabled)
            .ToListAsync();

        return new TeamContext(
            members.Values.ToList(),
            policies.ToDictionary(x => x.RoleId, x => x));
    }

    private async Task<HashSet<ulong>> GetOverlappingLeavesAsync(ulong guildId, DateTimeOffset fromUtc, DateTimeOffset toUtc)
    {
        var leaves = await db.TeamLeaveEntries
            .AsNoTracking()
            .Where(x => x.GuildId == guildId)
            .Where(x => x.StartUtc < toUtc)
            .Where(x => x.EndUtc == null || x.EndUtc > fromUtc)
            .Select(x => x.UserId)
            .Distinct()
            .ToListAsync();

        return leaves.ToHashSet();
    }

    private static bool MeetsMinimum(LevelUserActivitySummary summary, TeamActivityPolicy? policy)
    {
        if (policy is null || !policy.IsEnabled)
        {
            return true;
        }

        return summary.MessageCount >= policy.MinMessagesPerWeek
            && summary.VoiceMinutes >= policy.MinVoiceMinutesPerWeek;
    }

    private static double CalculateScore(int messages, int voiceMinutes)
    {
        return messages + (voiceMinutes * VoiceWeight);
    }

    private static string BuildMissingMinimumReason(TeamActivityPolicy? policy, int messages, int voiceMinutes)
    {
        if (policy is null)
        {
            return "Mindestaktivität wurde nicht erreicht.";
        }

        return $"Mindestaktivität verfehlt: Nachrichten {messages}/{policy.MinMessagesPerWeek}, Voice-Minuten {voiceMinutes}/{policy.MinVoiceMinutesPerWeek}.";
    }

    private static DateTimeOffset GetWeekStartUtc(DateTimeOffset timestamp)
    {
        var utcDate = timestamp.UtcDateTime.Date;
        var diff = ((int)utcDate.DayOfWeek + 6) % 7;
        var monday = utcDate.AddDays(-diff);
        return new DateTimeOffset(monday, TimeSpan.Zero);
    }

    private static async Task TrySendMinimumWarningDmAsync(
        DiscordClient client,
        ulong userId,
        DiscordGuild guild,
        DateTimeOffset weekStart,
        string reason)
    {
        try
        {
            var user = await client.GetUserAsync(userId);
            if (user is null)
            {
                return;
            }

            var dm = await user.CreateDmChannelAsync();
            var embed = new DiscordEmbedBuilder()
                .WithTitle("Team-Aktivitätsminimum nicht erreicht")
                .WithColor(CozyCoveUI.CozyWarning)
                .WithDescription(
                    $"Server: **{guild.Name}**\n" +
                    $"Woche ab: **{weekStart:yyyy-MM-dd}**\n" +
                    $"Details: {reason}")
                .WithTimestamp(DateTimeOffset.UtcNow);

            await dm.SendMessageAsync(new DiscordMessageBuilder().AddEmbed(embed));
        }
        catch
        {
            // Ignore DM errors (blocked DMs, privacy settings)
        }
    }

    private static DiscordEmbedBuilder BuildWeeklyReportEmbed(DiscordGuild guild, TeamWeeklyReportResult report)
    {
        var embed = new DiscordEmbedBuilder()
            .WithTitle($"Weekly Team Report - {guild.Name}")
            .WithColor(CozyCoveUI.CozyPrimary)
            .WithTimestamp(DateTimeOffset.UtcNow)
            .WithFeatureFooter("Teams", $"Week starting {report.WeekStartUtc:yyyy-MM-dd}");

        var topActiveText = report.TopActive.Count == 0
            ? "No activity data available."
            : string.Join("\n", report.TopActive.Select((entry, index) =>
            {
                var roleName = guild.Roles.GetValueOrDefault(entry.RoleId)?.Name ?? entry.RoleId.ToString();
                return $"**#{index + 1}** <@{entry.UserId}> ({roleName}) - Score {entry.CombinedScore:F1}, Msg {entry.Messages}, Voice {entry.VoiceMinutes}m";
            }));

        var underMinimumText = report.UnderMinimum.Count == 0
            ? "No active violations."
            : string.Join("\n", report.UnderMinimum.Select(entry =>
            {
                var roleName = guild.Roles.GetValueOrDefault(entry.RoleId)?.Name ?? entry.RoleId.ToString();
                return $"<@{entry.UserId}> ({roleName}) - Msg {entry.Messages}, Voice {entry.VoiceMinutes}m, Score {entry.CombinedScore:F1}";
            }));

        var absencesText = report.LeaveEntries.Count == 0
            ? "No active leaves."
            : string.Join("\n", report.LeaveEntries.Select(entry =>
            {
                var end = entry.EndUtc ?? report.WeekEndUtc;
                var duration = end > entry.StartUtc ? end - entry.StartUtc : TimeSpan.Zero;
                return $"<@{entry.UserId}> - {entry.Reason} ({(int)duration.TotalDays}d {duration.Hours}h)";
            }));

        var warningsText = report.Warnings.Count == 0
            ? "No new warnings."
            : string.Join("\n", report.Warnings.Select(entry =>
            {
                var source = entry.WarningType == TeamWarningType.MissingMinimum ? "Auto" : "Manual";
                return $"#{entry.Id} <@{entry.UserId}> ({source}) - {entry.Reason}";
            }));

        embed.AddField("Top-Aktive", TruncateForEmbed(topActiveText), false);
        embed.AddField("Unter Mindestaktivität", TruncateForEmbed(underMinimumText), false);
        embed.AddField("Aktive Abmeldungen", TruncateForEmbed(absencesText), false);
        embed.AddField("Neue Warnungen", TruncateForEmbed(warningsText), false);

        return embed;
    }

    private static string TruncateForEmbed(string value)
    {
        return value.Length <= 1024 ? value : value[..1021] + "...";
    }
}

public sealed record TeamRankingEntry(
    ulong UserId,
    ulong RoleId,
    int Messages,
    int VoiceMinutes,
    double Score,
    int MinMessages,
    int MinVoiceMinutes,
    bool MeetsMinimum,
    bool IsExcused);

public sealed record TeamLeaveStats(int Count, TimeSpan TotalDuration);

internal sealed record TeamContext(
    IReadOnlyList<TeamContextMember> Members,
    IReadOnlyDictionary<ulong, TeamActivityPolicy> PolicyByRoleId);

internal sealed record TeamContextMember(ulong UserId, ulong PrimaryRoleId);

public sealed record TeamWeeklyReportResult(
    ulong GuildId,
    DateTimeOffset WeekStartUtc,
    DateTimeOffset WeekEndUtc,
    IReadOnlyList<TeamActivityWeeklySnapshot> TopActive,
    IReadOnlyList<TeamActivityWeeklySnapshot> UnderMinimum,
    IReadOnlyList<TeamLeaveEntry> LeaveEntries,
    IReadOnlyList<TeamWarning> Warnings,
    IReadOnlyCollection<ulong> TrackedRoleIds);

public sealed record TeamRoleProgress(
    ulong RoleId,
    int MinMessagesPerWeek,
    int MinVoiceMinutesPerWeek,
    int RemainingMessages,
    int RemainingVoiceMinutes,
    bool MeetsMessageMinimum,
    bool MeetsVoiceMinimum);

public sealed record TeamWarningSummary(
    int WarningId,
    string Reason,
    TeamWarningType WarningType,
    DateTimeOffset CreatedAtUtc,
    bool IsResolved);

public sealed record TeamUserWeeklyStatus(
    ulong UserId,
    int MessageCount,
    int VoiceMinutes,
    double Score,
    int ActiveWarningCount,
    IReadOnlyList<TeamWarningSummary> RecentWarnings,
    IReadOnlyList<TeamRoleProgress> RoleProgress);