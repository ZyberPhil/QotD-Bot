using System.Collections.Concurrent;
using DSharpPlus.Entities;
using Microsoft.EntityFrameworkCore;
using QotD.Bot.Data;
using QotD.Bot.Data.Models;
using QotD.Bot.Features.LinkModeration.Services;

namespace QotD.Bot.Features.AutoModeration.Services;

public sealed class AutoModerationMessageDecision
{
    public bool ShouldBlock { get; init; }
    public string RuleKey { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
    public string? Evidence { get; init; }
    public AutoModerationConfig? Config { get; init; }
}

public sealed class RaidJoinDecision
{
    public bool TriggeredLockdown { get; init; }
    public int JoinCountInWindow { get; init; }
    public AutoModerationConfig? Config { get; init; }
}

public sealed class AutoModerationService
{
    private static readonly ConcurrentDictionary<ulong, ConcurrentQueue<DateTimeOffset>> JoinWindows = new();

    private readonly AppDbContext _db;

    public AutoModerationService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<AutoModerationConfig> GetOrCreateConfigAsync(ulong guildId)
    {
        var config = await _db.Set<AutoModerationConfig>().FirstOrDefaultAsync(x => x.GuildId == guildId);
        if (config is not null)
        {
            return config;
        }

        config = new AutoModerationConfig
        {
            GuildId = guildId
        };

        _db.Set<AutoModerationConfig>().Add(config);
        await _db.SaveChangesAsync();
        return config;
    }

    public async Task<AutoModerationConfig?> GetConfigAsync(ulong guildId)
    {
        return await _db.Set<AutoModerationConfig>().FirstOrDefaultAsync(x => x.GuildId == guildId);
    }

    public async Task<RaidJoinDecision> RegisterJoinAndEvaluateRaidAsync(ulong guildId)
    {
        var config = await _db.Set<AutoModerationConfig>().FirstOrDefaultAsync(x => x.GuildId == guildId);
        if (config is null || !config.IsEnabled || !config.RaidModeEnabled)
        {
            return new RaidJoinDecision
            {
                TriggeredLockdown = false,
                JoinCountInWindow = 0,
                Config = config
            };
        }

        var now = DateTimeOffset.UtcNow;
        await TryCloseExpiredLockdownAsync(config, now);

        var queue = JoinWindows.GetOrAdd(guildId, _ => new ConcurrentQueue<DateTimeOffset>());
        queue.Enqueue(now);

        while (queue.TryPeek(out var first) && (now - first).TotalSeconds > config.RaidWindowSeconds)
        {
            queue.TryDequeue(out _);
        }

        var joinCount = queue.Count;
        if (config.IsLockdownActive || joinCount < config.RaidJoinThreshold)
        {
            return new RaidJoinDecision
            {
                TriggeredLockdown = false,
                JoinCountInWindow = joinCount,
                Config = config
            };
        }

        config.IsLockdownActive = true;
        config.LockdownActivatedAtUtc = now;
        config.LockdownEndsAtUtc = now.AddMinutes(config.RaidLockdownMinutes);
        config.UpdatedAtUtc = now;

        _db.Set<AutoModerationRaidIncident>().Add(new AutoModerationRaidIncident
        {
            GuildId = guildId,
            TriggerJoinCount = joinCount,
            WindowSeconds = config.RaidWindowSeconds,
            Notes = "Automatic lockdown triggered by rapid joins."
        });

        await AddAuditEntryAsync(new AutoModerationAuditEntry
        {
            GuildId = guildId,
            Action = AutoModerationAuditAction.LockdownActivated,
            RuleKey = "raid.join-rate",
            Reason = "Raid threshold reached. Lockdown activated.",
            Evidence = $"joinCount={joinCount};windowSeconds={config.RaidWindowSeconds};threshold={config.RaidJoinThreshold}"
        });

        await _db.SaveChangesAsync();

        return new RaidJoinDecision
        {
            TriggeredLockdown = true,
            JoinCountInWindow = joinCount,
            Config = config
        };
    }

    public async Task<AutoModerationMessageDecision> EvaluateMessageAsync(DiscordMember member, DiscordMessage message)
    {
        var guildId = member.Guild.Id;
        var config = await _db.Set<AutoModerationConfig>().FirstOrDefaultAsync(x => x.GuildId == guildId);
        if (config is null || !config.IsEnabled)
        {
            return new AutoModerationMessageDecision
            {
                ShouldBlock = false,
                Config = config
            };
        }

        var now = DateTimeOffset.UtcNow;
        await TryCloseExpiredLockdownAsync(config, now);

        if (config.IsLockdownActive)
        {
            if (config.RestrictToVerifiedRoleDuringLockdown && config.VerifiedRoleId is > 0)
            {
                var hasRole = member.Roles.Any(x => x.Id == config.VerifiedRoleId.Value);
                if (!hasRole)
                {
                    return new AutoModerationMessageDecision
                    {
                        ShouldBlock = true,
                        RuleKey = "raid.lockdown.verified-role",
                        Reason = "Lockdown active: only verified members may post.",
                        Config = config
                    };
                }
            }

            var accountAgeHours = (now - GetAccountCreatedAt(member.Id)).TotalHours;
            if (accountAgeHours < config.LockdownMinAccountAgeHours)
            {
                return new AutoModerationMessageDecision
                {
                    ShouldBlock = true,
                    RuleKey = "raid.lockdown.account-age",
                    Reason = $"Lockdown active: account must be at least {config.LockdownMinAccountAgeHours}h old.",
                    Evidence = $"accountAgeHours={accountAgeHours:F2}",
                    Config = config
                };
            }
        }

        var links = LinkModerationService.ExtractLinks(message.Content);
        if (links.Count == 0)
        {
            return new AutoModerationMessageDecision
            {
                ShouldBlock = false,
                Config = config
            };
        }

        var accountAgeDays = (now - GetAccountCreatedAt(member.Id)).TotalDays;
        if (config.EnforceAccountAgeForLinks && accountAgeDays < config.MinAccountAgeDaysForLinks)
        {
            return new AutoModerationMessageDecision
            {
                ShouldBlock = true,
                RuleKey = "gate.account-age.links",
                Reason = $"Account must be at least {config.MinAccountAgeDaysForLinks} days old to post links.",
                Evidence = $"accountAgeDays={accountAgeDays:F2};links={string.Join(",", links)}",
                Config = config
            };
        }

        if (config.EnforceServerAgeForLinks)
        {
            var serverAgeHours = (now - member.JoinedAt).TotalHours;
            if (serverAgeHours < config.MinServerAgeHoursForLinks)
            {
                return new AutoModerationMessageDecision
                {
                    ShouldBlock = true,
                    RuleKey = "gate.server-age.links",
                    Reason = $"Member must be on the server for at least {config.MinServerAgeHoursForLinks}h to post links.",
                    Evidence = $"serverAgeHours={serverAgeHours:F2};links={string.Join(",", links)}",
                    Config = config
                };
            }
        }

        return new AutoModerationMessageDecision
        {
            ShouldBlock = false,
            Config = config
        };
    }

    public async Task AddAuditEntryAsync(AutoModerationAuditEntry entry)
    {
        _db.Set<AutoModerationAuditEntry>().Add(entry);
        await _db.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<AutoModerationAuditEntry>> GetRecentAuditEntriesAsync(ulong guildId, int limit)
    {
        var normalized = Math.Clamp(limit, 1, 50);
        return await _db.Set<AutoModerationAuditEntry>()
            .AsNoTracking()
            .Where(x => x.GuildId == guildId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(normalized)
            .ToListAsync();
    }

    private async Task TryCloseExpiredLockdownAsync(AutoModerationConfig config, DateTimeOffset now)
    {
        if (!config.IsLockdownActive || config.LockdownEndsAtUtc is null || config.LockdownEndsAtUtc > now)
        {
            return;
        }

        config.IsLockdownActive = false;
        config.LockdownActivatedAtUtc = null;
        config.LockdownEndsAtUtc = null;
        config.UpdatedAtUtc = now;

        var incident = await _db.Set<AutoModerationRaidIncident>()
            .Where(x => x.GuildId == config.GuildId && x.EndedAtUtc == null)
            .OrderByDescending(x => x.StartedAtUtc)
            .FirstOrDefaultAsync();

        if (incident is not null)
        {
            incident.EndedAtUtc = now;
        }

        _db.Set<AutoModerationAuditEntry>().Add(new AutoModerationAuditEntry
        {
            GuildId = config.GuildId,
            Action = AutoModerationAuditAction.LockdownEnded,
            RuleKey = "raid.lockdown.expired",
            Reason = "Automatic lockdown ended because timeout elapsed."
        });

        await _db.SaveChangesAsync();
    }

    private static DateTimeOffset GetAccountCreatedAt(ulong userId)
    {
        const long discordEpoch = 1420070400000;
        var unixMs = (long)(userId >> 22) + discordEpoch;
        return DateTimeOffset.FromUnixTimeMilliseconds(unixMs);
    }
}
