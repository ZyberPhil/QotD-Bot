using DSharpPlus;
using DSharpPlus.Entities;
using Microsoft.EntityFrameworkCore;
using QotD.Bot.Data;
using QotD.Bot.Data.Models;
using QotD.Bot.Features.Leveling.Services;

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
            .Where(x => x.GuildId == guildId && x.IsActive);

        if (userId.HasValue)
        {
            query = query.Where(x => x.UserId == userId.Value);
        }

        return await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(50)
            .ToListAsync();
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
                .WithColor(DiscordColor.Orange)
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