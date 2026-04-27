using System.Text;
using DSharpPlus;
using DSharpPlus.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QotD.Bot.Data;
using QotD.Bot.Data.Models;

namespace QotD.Bot.Features.Tickets.Services;

public sealed class TicketService
{
    private readonly AppDbContext _db;
    private readonly ILogger<TicketService> _logger;

    public TicketService(AppDbContext db, ILogger<TicketService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<TicketConfig> UpsertConfigAsync(
        ulong guildId,
        ulong categoryId,
        IReadOnlyCollection<ulong> staffRoleIds,
        int maxOpenTicketsPerUser,
        int defaultSlaMinutes)
    {
        var config = await _db.TicketConfigs
            .Include(x => x.StaffRoles)
            .FirstOrDefaultAsync(x => x.GuildId == guildId);

        if (config is null)
        {
            config = new TicketConfig
            {
                GuildId = guildId,
                CategoryId = categoryId,
                MaxOpenTicketsPerUser = Math.Clamp(maxOpenTicketsPerUser, 1, 10),
                DefaultSlaMinutes = Math.Clamp(defaultSlaMinutes, 15, 1440),
                IsEnabled = true,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            };

            _db.TicketConfigs.Add(config);
        }
        else
        {
            config.CategoryId = categoryId;
            config.MaxOpenTicketsPerUser = Math.Clamp(maxOpenTicketsPerUser, 1, 10);
            config.DefaultSlaMinutes = Math.Clamp(defaultSlaMinutes, 15, 1440);
            config.IsEnabled = true;
            config.UpdatedAtUtc = DateTimeOffset.UtcNow;
        }

        var distinctRoleIds = staffRoleIds.Distinct().Take(10).ToHashSet();
        config.StaffRoles.Clear();
        foreach (var roleId in distinctRoleIds)
        {
            config.StaffRoles.Add(new TicketStaffRole
            {
                GuildId = guildId,
                RoleId = roleId,
                CreatedAtUtc = DateTimeOffset.UtcNow
            });
        }

        await _db.SaveChangesAsync();
        return config;
    }

    public async Task<Ticket?> CreateTicketAsync(
        DiscordGuild guild,
        DiscordMember creator,
        TicketType type,
        TicketPriority priority,
        string? subject)
    {
        var config = await _db.TicketConfigs
            .Include(x => x.StaffRoles)
            .FirstOrDefaultAsync(x => x.GuildId == guild.Id && x.IsEnabled);

        if (config is null || config.CategoryId == 0)
        {
            return null;
        }

        var openCount = await _db.Tickets.CountAsync(x =>
            x.GuildId == guild.Id &&
            x.CreatedByUserId == creator.Id &&
            x.Status != TicketStatus.Closed);

        if (openCount >= config.MaxOpenTicketsPerUser)
        {
            return null;
        }

        var nextTicketNumber = await _db.Tickets
            .Where(x => x.GuildId == guild.Id)
            .Select(x => (int?)x.TicketNumber)
            .MaxAsync() ?? 0;
        nextTicketNumber += 1;

        var category = guild.Channels.GetValueOrDefault(config.CategoryId);
        if (category is null)
        {
            return null;
        }

        var channelName = BuildTicketChannelName(nextTicketNumber, type);
        var channel = await guild.CreateChannelAsync(channelName, DiscordChannelType.Text, category);

        await channel.AddOverwriteAsync(guild.EveryoneRole, deny: DiscordPermission.ViewChannel);
        await channel.AddOverwriteAsync(creator, allow: DiscordPermission.ViewChannel | DiscordPermission.SendMessages | DiscordPermission.ReadMessageHistory);

        foreach (var staffRoleId in config.StaffRoles.Select(x => x.RoleId))
        {
            var role = guild.Roles.GetValueOrDefault(staffRoleId);
            if (role is null)
            {
                continue;
            }

            await channel.AddOverwriteAsync(role, allow: DiscordPermission.ViewChannel | DiscordPermission.SendMessages | DiscordPermission.ReadMessageHistory | DiscordPermission.ManageMessages);
        }

        var ticket = new Ticket
        {
            GuildId = guild.Id,
            TicketNumber = nextTicketNumber,
            ChannelId = channel.Id,
            CreatedByUserId = creator.Id,
            Type = type,
            Priority = priority,
            Status = TicketStatus.Open,
            Subject = string.IsNullOrWhiteSpace(subject) ? null : subject.Trim(),
            CreatedAtUtc = DateTimeOffset.UtcNow,
            LastActivityAtUtc = DateTimeOffset.UtcNow
        };

        _db.Tickets.Add(ticket);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Created ticket #{TicketNumber} in guild {GuildId} by {UserId}", ticket.TicketNumber, guild.Id, creator.Id);
        return ticket;
    }

    public async Task<Ticket?> GetTicketByChannelAsync(ulong guildId, ulong channelId)
    {
        return await _db.Tickets.FirstOrDefaultAsync(x => x.GuildId == guildId && x.ChannelId == channelId);
    }

    public async Task<bool> ClaimTicketAsync(ulong guildId, ulong channelId, ulong claimerUserId)
    {
        var ticket = await _db.Tickets.FirstOrDefaultAsync(x => x.GuildId == guildId && x.ChannelId == channelId);
        if (ticket is null || ticket.Status == TicketStatus.Closed)
        {
            return false;
        }

        ticket.ClaimedByUserId = claimerUserId;
        ticket.ClaimedAtUtc = DateTimeOffset.UtcNow;
        ticket.Status = TicketStatus.Claimed;
        ticket.LastActivityAtUtc = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> CloseTicketAsync(ulong guildId, ulong channelId, ulong closedByUserId, string reason)
    {
        var ticket = await _db.Tickets.FirstOrDefaultAsync(x => x.GuildId == guildId && x.ChannelId == channelId);
        if (ticket is null || ticket.Status == TicketStatus.Closed)
        {
            return false;
        }

        ticket.Status = TicketStatus.Closed;
        ticket.CloseReason = string.IsNullOrWhiteSpace(reason) ? "No reason provided." : reason.Trim();
        ticket.ClosedAtUtc = DateTimeOffset.UtcNow;
        ticket.LastActivityAtUtc = DateTimeOffset.UtcNow;
        ticket.ClaimedByUserId ??= closedByUserId;

        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ReopenTicketAsync(ulong guildId, ulong channelId)
    {
        var ticket = await _db.Tickets.FirstOrDefaultAsync(x => x.GuildId == guildId && x.ChannelId == channelId);
        if (ticket is null || ticket.Status != TicketStatus.Closed)
        {
            return false;
        }

        ticket.Status = ticket.ClaimedByUserId.HasValue ? TicketStatus.Claimed : TicketStatus.Open;
        ticket.CloseReason = null;
        ticket.ClosedAtUtc = null;
        ticket.LastActivityAtUtc = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<TicketTranscript?> CreateTranscriptAsync(DiscordChannel channel, ulong actorUserId)
    {
        var ticket = await _db.Tickets.FirstOrDefaultAsync(x => x.GuildId == channel.GuildId && x.ChannelId == channel.Id);
        if (ticket is null)
        {
            return null;
        }

        var ordered = new List<DiscordMessage>(100);
        await foreach (var message in channel.GetMessagesAsync())
        {
            ordered.Add(message);
            if (ordered.Count >= 100)
            {
                break;
            }
        }

        ordered = ordered.OrderBy(x => x.CreationTimestamp.UtcDateTime).ToList();

        var sb = new StringBuilder();
        sb.AppendLine($"Transcript for Ticket #{ticket.TicketNumber}");
        sb.AppendLine($"GuildId: {ticket.GuildId}");
        sb.AppendLine($"ChannelId: {ticket.ChannelId}");
        sb.AppendLine($"GeneratedAtUtc: {DateTimeOffset.UtcNow:O}");
        sb.AppendLine(new string('-', 60));

        foreach (var message in ordered)
        {
            var authorName = message.Author?.Username ?? "UnknownUser";
            sb.AppendLine($"[{message.CreationTimestamp.UtcDateTime:yyyy-MM-dd HH:mm:ss}] {authorName}: {message.Content}");
        }

        var existing = await _db.TicketTranscripts.FirstOrDefaultAsync(x => x.GuildId == ticket.GuildId && x.TicketId == ticket.Id);
        if (existing is null)
        {
            existing = new TicketTranscript
            {
                GuildId = ticket.GuildId,
                TicketId = ticket.Id,
                TicketChannelId = ticket.ChannelId,
                FileName = $"ticket-{ticket.TicketNumber}-transcript.txt",
                Content = sb.ToString(),
                GeneratedByUserId = actorUserId,
                GeneratedAtUtc = DateTimeOffset.UtcNow
            };

            _db.TicketTranscripts.Add(existing);
        }
        else
        {
            existing.Content = sb.ToString();
            existing.GeneratedByUserId = actorUserId;
            existing.GeneratedAtUtc = DateTimeOffset.UtcNow;
        }

        await _db.SaveChangesAsync();
        return existing;
    }

    private static string BuildTicketChannelName(int ticketNumber, TicketType type)
    {
        var typePrefix = type switch
        {
            TicketType.Support => "support",
            TicketType.Report => "report",
            TicketType.Bewerbung => "bewerbung",
            _ => "ticket"
        };

        return $"{typePrefix}-{ticketNumber:D4}";
    }
}
