using System.Text.RegularExpressions;
using DSharpPlus;
using Microsoft.EntityFrameworkCore;
using QotD.Bot.Data;
using QotD.Bot.Data.Models;

namespace QotD.Bot.Features.LinkModeration.Services;

public sealed class LinkModerationDecision
{
    public bool IsEnabled { get; init; }
    public bool ShouldBlock { get; init; }
    public LinkFilterConfig? Config { get; init; }
    public IReadOnlyList<string> BlockedLinks { get; init; } = Array.Empty<string>();
}

public sealed class LinkModerationService
{
    private static readonly Regex LinkRegex = new(
        @"(?:(?:https?://)|(?:www\.))?[a-z0-9][a-z0-9.-]*\.[a-z]{2,}(?:/[^\s<>()]*)?",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex DiscordInviteRegex = new(
        @"(?:https?://)?(?:www\.)?(?:discord\.gg/[a-z0-9-]+|discord(?:app)?\.com/invite/[a-z0-9-]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly AppDbContext _db;

    public LinkModerationService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<LinkFilterConfig> GetOrCreateConfigAsync(ulong guildId)
    {
        var config = await _db.LinkFilterConfigs.FirstOrDefaultAsync(x => x.GuildId == guildId);
        if (config is null)
        {
            config = new LinkFilterConfig
            {
                GuildId = guildId
            };
            _db.LinkFilterConfigs.Add(config);
            await _db.SaveChangesAsync();
        }

        return config;
    }

    public async Task<IReadOnlyList<LinkFilterRule>> GetRulesAsync(ulong guildId)
    {
        return await _db.LinkFilterRules
            .AsNoTracking()
            .Where(x => x.GuildId == guildId)
            .OrderBy(x => x.NormalizedDomain)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<LinkFilterBypassRole>> GetBypassRolesAsync(ulong guildId)
    {
        return await _db.LinkFilterBypassRoles
            .AsNoTracking()
            .Where(x => x.GuildId == guildId)
            .OrderBy(x => x.RoleId)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<LinkFilterBypassChannel>> GetBypassChannelsAsync(ulong guildId)
    {
        return await _db.LinkFilterBypassChannels
            .AsNoTracking()
            .Where(x => x.GuildId == guildId)
            .OrderBy(x => x.ChannelId)
            .ToListAsync();
    }

    public async Task<LinkModerationDecision> EvaluateAsync(ulong guildId, ulong channelId, IReadOnlyCollection<ulong> roleIds, string content)
    {
        var config = await _db.LinkFilterConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.GuildId == guildId);

        if (config is null || !config.IsEnabled)
        {
            return new LinkModerationDecision
            {
                IsEnabled = false,
                ShouldBlock = false,
                Config = config
            };
        }

        var isBypassChannel = await _db.LinkFilterBypassChannels
            .AsNoTracking()
            .AnyAsync(x => x.GuildId == guildId && x.ChannelId == channelId);

        if (isBypassChannel)
        {
            return new LinkModerationDecision
            {
                IsEnabled = true,
                ShouldBlock = false,
                Config = config
            };
        }

        if (roleIds.Count > 0)
        {
            var bypassRoles = await _db.LinkFilterBypassRoles
                .AsNoTracking()
                .Where(x => x.GuildId == guildId)
                .Select(x => x.RoleId)
                .ToListAsync();

            if (bypassRoles.Any(roleIds.Contains))
            {
                return new LinkModerationDecision
                {
                    IsEnabled = true,
                    ShouldBlock = false,
                    Config = config
                };
            }
        }

        var links = ExtractLinks(content);
        if (links.Count == 0)
        {
            return new LinkModerationDecision
            {
                IsEnabled = true,
                ShouldBlock = false,
                Config = config
            };
        }

        var ruleDomains = await _db.LinkFilterRules
            .AsNoTracking()
            .Where(x => x.GuildId == guildId)
            .Select(x => x.NormalizedDomain)
            .ToListAsync();

        var blockedLinks = new List<string>();

        foreach (var link in links)
        {
            if (IsDiscordInvite(link))
            {
                blockedLinks.Add(link);
                continue;
            }

            var domain = NormalizeDomain(link);
            if (string.IsNullOrWhiteSpace(domain))
            {
                continue;
            }

            var listed = ruleDomains.Any(x => DomainMatches(domain, x));

            if (config.Mode == LinkFilterMode.Whitelist && !listed)
            {
                blockedLinks.Add(link);
                continue;
            }

            if (config.Mode == LinkFilterMode.Blacklist && listed)
            {
                blockedLinks.Add(link);
            }
        }

        return new LinkModerationDecision
        {
            IsEnabled = true,
            ShouldBlock = blockedLinks.Count > 0,
            Config = config,
            BlockedLinks = blockedLinks
        };
    }

    public static IReadOnlyList<string> ExtractLinks(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return Array.Empty<string>();
        }

        var result = LinkRegex.Matches(content)
            .Select(match => match.Value.Trim().TrimEnd('.', ',', ';', ':', '!', '?', ')', ']'))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return result;
    }

    public static bool IsDiscordInvite(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return DiscordInviteRegex.IsMatch(value);
    }

    public static string? NormalizeDomain(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        var candidate = trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            ? trimmed
            : $"https://{trimmed}";

        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri))
        {
            return null;
        }

        var host = uri.Host.Trim().ToLowerInvariant().TrimEnd('.');
        if (host.StartsWith("www.", StringComparison.Ordinal))
        {
            host = host[4..];
        }

        return string.IsNullOrWhiteSpace(host) ? null : host;
    }

    public static bool DomainMatches(string domain, string rule)
    {
        if (string.IsNullOrWhiteSpace(domain) || string.IsNullOrWhiteSpace(rule))
        {
            return false;
        }

        if (domain.Equals(rule, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return domain.EndsWith($".{rule}", StringComparison.OrdinalIgnoreCase);
    }
}
