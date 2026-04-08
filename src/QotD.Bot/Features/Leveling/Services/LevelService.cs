using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QotD.Bot.Features.Leveling.Data;
using QotD.Bot.Features.Leveling.Data.Models;

namespace QotD.Bot.Features.Leveling.Services;

public sealed class LevelService
{
    private const int MinMessageXp = 15;
    private const int MaxMessageXp = 25;
    private static readonly TimeSpan MessageXpCooldown = TimeSpan.FromSeconds(60);

    private readonly IServiceScopeFactory _scopeFactory;

    public LevelService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task<LevelGrantResult> GrantMessageXpAsync(ulong guildId, ulong userId, DateTimeOffset nowUtc)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LevelDatabaseContext>();

        var guildIdValue = checked((long)guildId);
        var userIdValue = checked((long)userId);

        var entry = await db.LevelUserStats
            .FirstOrDefaultAsync(x => x.GuildId == guildIdValue && x.UserId == userIdValue);

        if (entry is null)
        {
            entry = new LevelUserStats
            {
                GuildId = guildIdValue,
                UserId = userIdValue,
                XP = 0,
                Level = 0,
                MessageCount = 0,
                LastMessageXpAtUtc = null
            };

            db.LevelUserStats.Add(entry);
        }

        if (entry.LastMessageXpAtUtc is not null && nowUtc - entry.LastMessageXpAtUtc.Value < MessageXpCooldown)
        {
            return new LevelGrantResult(false, entry.Level, entry.XP, 0);
        }

        var gainedXp = Random.Shared.Next(MinMessageXp, MaxMessageXp + 1);
        entry.XP += gainedXp;
        entry.MessageCount += 1;
        entry.LastMessageXpAtUtc = nowUtc;

        var previousLevel = entry.Level;
        while (entry.XP >= GetTotalXpForLevel(entry.Level + 1))
        {
            entry.Level += 1;
        }

        await db.SaveChangesAsync();

        return new LevelGrantResult(entry.Level > previousLevel, entry.Level, entry.XP, gainedXp);
    }

    public async Task<LevelUserSnapshot> GetUserSnapshotAsync(ulong guildId, ulong userId)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LevelDatabaseContext>();

        var guildIdValue = checked((long)guildId);
        var userIdValue = checked((long)userId);

        var entry = await db.LevelUserStats
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.GuildId == guildIdValue && x.UserId == userIdValue);

        if (entry is null)
        {
            return CreateSnapshot(userId, 0, 0, 0, 0);
        }

        var rank = await CalculateRankAsync(db, guildIdValue, entry);
        return CreateSnapshot(userId, entry.Level, entry.XP, entry.MessageCount, rank);
    }

    public async Task<IReadOnlyList<LevelLeaderboardEntry>> GetLeaderboardAsync(ulong guildId, int top = 10)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LevelDatabaseContext>();

        var guildIdValue = checked((long)guildId);
        var normalizedTop = Math.Clamp(top, 1, 25);

        var entries = await db.LevelUserStats
            .AsNoTracking()
            .Where(x => x.GuildId == guildIdValue)
            .OrderByDescending(x => x.Level)
            .ThenByDescending(x => x.XP)
            .ThenByDescending(x => x.MessageCount)
            .Take(normalizedTop)
            .ToListAsync();

        var result = new List<LevelLeaderboardEntry>(entries.Count);
        var rank = 1;

        foreach (var entry in entries)
        {
            var snapshot = CreateSnapshot((ulong)entry.UserId, entry.Level, entry.XP, entry.MessageCount, rank);
            result.Add(new LevelLeaderboardEntry(snapshot, rank));
            rank += 1;
        }

        return result;
    }

    public async Task<long> GetLevelUpChannelAsync(ulong guildId)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LevelDatabaseContext>();

        var guildIdValue = checked((long)guildId);
        var config = await db.LevelingConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.GuildId == guildIdValue && x.IsEnabled);

        return config?.LevelUpChannelId ?? 0;
    }

    public async Task SetLevelUpChannelAsync(ulong guildId, ulong channelId)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LevelDatabaseContext>();

        var guildIdValue = checked((long)guildId);
        var channelIdValue = checked((long)channelId);

        var config = await db.LevelingConfigs
            .FirstOrDefaultAsync(x => x.GuildId == guildIdValue);

        if (config is null)
        {
            config = new LevelingConfig
            {
                GuildId = guildIdValue,
                LevelUpChannelId = channelIdValue,
                IsEnabled = true
            };
            db.LevelingConfigs.Add(config);
        }
        else
        {
            config.LevelUpChannelId = channelIdValue;
            config.IsEnabled = true;
        }

        await db.SaveChangesAsync();
    }

    public async Task DisableLevelUpNotificationsAsync(ulong guildId)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LevelDatabaseContext>();

        var guildIdValue = checked((long)guildId);
        var config = await db.LevelingConfigs
            .FirstOrDefaultAsync(x => x.GuildId == guildIdValue);

        if (config is not null)
        {
            config.IsEnabled = false;
            await db.SaveChangesAsync();
        }
    }

    public static int GetXpRequiredForLevel(int level)
    {
        if (level < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(level), "Level must be >= 0.");
        }

        return (5 * level * level) + (50 * level) + 100;
    }

    public static int GetTotalXpForLevel(int level)
    {
        if (level < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(level), "Level must be >= 0.");
        }

        var total = 0;
        for (var current = 0; current < level; current += 1)
        {
            total += GetXpRequiredForLevel(current);
        }

        return total;
    }

    private static async Task<int> CalculateRankAsync(LevelDatabaseContext db, long guildId, LevelUserStats entry)
    {
        var higherRankedUsers = await db.LevelUserStats
            .AsNoTracking()
            .CountAsync(x =>
                x.GuildId == guildId &&
                (x.Level > entry.Level ||
                 (x.Level == entry.Level && x.XP > entry.XP) ||
                 (x.Level == entry.Level && x.XP == entry.XP && x.MessageCount > entry.MessageCount)));

        return higherRankedUsers + 1;
    }

    private static LevelUserSnapshot CreateSnapshot(ulong userId, int level, int totalXp, int messageCount, int rank)
    {
        var currentLevelStartXp = GetTotalXpForLevel(level);
        var xpRequiredThisLevel = GetXpRequiredForLevel(level);
        var xpIntoCurrentLevel = Math.Max(totalXp - currentLevelStartXp, 0);

        return new LevelUserSnapshot(
            userId,
            level,
            totalXp,
            xpIntoCurrentLevel,
            xpRequiredThisLevel,
            messageCount,
            rank);
    }
}

public sealed record LevelGrantResult(bool LeveledUp, int NewLevel, int TotalXp, int GainedXp);

public sealed record LevelUserSnapshot(
    ulong UserId,
    int Level,
    int TotalXp,
    int CurrentLevelXp,
    int RequiredLevelXp,
    int MessageCount,
    int Rank);

public sealed record LevelLeaderboardEntry(LevelUserSnapshot Snapshot, int Rank);
