using System.Net;
using System.Security.Cryptography;
using System.Text;
using DSharpPlus.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using QotD.Bot.Data;
using QotD.Bot.Data.Models;

namespace QotD.Bot.Features.Teams.Services;

public sealed class ModerationService
{
    private readonly AppDbContext _db;
    private readonly string _ipBanSalt;

    public ModerationService(AppDbContext db, IConfiguration configuration)
    {
        _db = db;
        _ipBanSalt = configuration["Moderation:IpBanSalt"] ?? string.Empty;
    }

    public async Task BanUserAsync(DiscordGuild guild, DiscordUser targetUser, ulong moderatorUserId, string reason)
    {
        if (guild.Id == 0)
        {
            throw new InvalidOperationException("Guild ist nicht gueltig.");
        }

        if (targetUser.Id == moderatorUserId)
        {
            throw new InvalidOperationException("Du kannst dich nicht selbst bannen.");
        }

        await guild.BanMemberAsync(targetUser, TimeSpan.Zero, reason);
    }

    public async Task UnbanUserAsync(DiscordGuild guild, ulong userId, string reason)
    {
        if (guild.Id == 0)
        {
            throw new InvalidOperationException("Guild ist nicht gueltig.");
        }

        await guild.UnbanMemberAsync(userId, reason);
    }

    public async Task<(bool Added, GuildIpBanEntry? Entry)> AddIpBanAsync(ulong guildId, string inputIp, string? note, ulong createdByUserId)
    {
        var normalizedIp = NormalizeIp(inputIp);
        var hash = ComputeIpHash(normalizedIp);

        var existing = await _db.Set<GuildIpBanEntry>()
            .FirstOrDefaultAsync(x => x.GuildId == guildId && x.IpHash == hash);

        if (existing is not null)
        {
            return (false, existing);
        }

        var entry = new GuildIpBanEntry
        {
            GuildId = guildId,
            IpHash = hash,
            MaskedIp = MaskIp(normalizedIp),
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            CreatedByUserId = createdByUserId
        };

        _db.Set<GuildIpBanEntry>().Add(entry);
        await _db.SaveChangesAsync();

        return (true, entry);
    }

    public async Task<bool> RemoveIpBanAsync(ulong guildId, string inputIp)
    {
        var normalizedIp = NormalizeIp(inputIp);
        var hash = ComputeIpHash(normalizedIp);

        var entry = await _db.Set<GuildIpBanEntry>()
            .FirstOrDefaultAsync(x => x.GuildId == guildId && x.IpHash == hash);

        if (entry is null)
        {
            return false;
        }

        _db.Set<GuildIpBanEntry>().Remove(entry);
        await _db.SaveChangesAsync();
        return true;
    }

    public Task<List<GuildIpBanEntry>> GetIpBansAsync(ulong guildId)
    {
        return _db.Set<GuildIpBanEntry>()
            .AsNoTracking()
            .Where(x => x.GuildId == guildId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync();
    }

    private static string NormalizeIp(string inputIp)
    {
        if (string.IsNullOrWhiteSpace(inputIp))
        {
            throw new InvalidOperationException("Bitte gib eine gueltige IP an.");
        }

        if (!IPAddress.TryParse(inputIp.Trim(), out var parsed))
        {
            throw new InvalidOperationException("Ungueltiges IP-Format. Erlaubt sind IPv4 und IPv6.");
        }

        if (parsed.IsIPv4MappedToIPv6)
        {
            parsed = parsed.MapToIPv4();
        }

        return parsed.ToString();
    }

    private string ComputeIpHash(string normalizedIp)
    {
        var raw = string.IsNullOrEmpty(_ipBanSalt)
            ? normalizedIp
            : $"{_ipBanSalt}:{normalizedIp}";

        var bytes = Encoding.UTF8.GetBytes(raw);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }

    private static string MaskIp(string normalizedIp)
    {
        if (IPAddress.TryParse(normalizedIp, out var parsed))
        {
            if (parsed.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            {
                var parts = normalizedIp.Split('.');
                if (parts.Length == 4)
                {
                    return $"{parts[0]}.{parts[1]}.{parts[2]}.x";
                }
            }

            if (parsed.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
            {
                var bytes = parsed.GetAddressBytes();
                var p1 = (bytes[0] << 8) | bytes[1];
                var p2 = (bytes[2] << 8) | bytes[3];
                var p3 = (bytes[4] << 8) | bytes[5];
                var p4 = (bytes[6] << 8) | bytes[7];
                return $"{p1:x4}:{p2:x4}:{p3:x4}:{p4:x4}:...";
            }
        }

        return "masked";
    }
}
