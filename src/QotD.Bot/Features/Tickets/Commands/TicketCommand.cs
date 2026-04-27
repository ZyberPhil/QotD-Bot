using System.ComponentModel;
using DSharpPlus;
using DSharpPlus.Commands;
using DSharpPlus.Commands.ContextChecks;
using DSharpPlus.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QotD.Bot.Data;
using QotD.Bot.Data.Models;
using QotD.Bot.Features.Tickets.Services;
using QotD.Bot.UI;

namespace QotD.Bot.Features.Tickets.Commands;

[Command("ticket")]
[Description("Ticket commands for support flow")]
public sealed class TicketCommand
{
    private readonly TicketService _ticketService;

    public TicketCommand(TicketService ticketService)
    {
        _ticketService = ticketService;
    }

    [Command("open")]
    [Description("Open a new ticket")]
    public async ValueTask OpenAsync(
        CommandContext ctx,
        [Description("Type: support, report, bewerbung")] string type = "support",
        [Description("Priority: low, medium, high")] string priority = "medium",
        [Description("Optional subject")] string? subject = null)
    {
        if (ctx.Guild is null || ctx.Member is null)
        {
            await ctx.RespondAsync("This command can only be used in a guild.");
            return;
        }

        if (!TryParseType(type, out var parsedType))
        {
            await ctx.RespondAsync("Unknown type. Use: support, report, bewerbung.");
            return;
        }

        if (!TryParsePriority(priority, out var parsedPriority))
        {
            await ctx.RespondAsync("Unknown priority. Use: low, medium, high.");
            return;
        }

        await ctx.DeferResponseAsync();
        var ticket = await _ticketService.CreateTicketAsync(ctx.Guild, ctx.Member, parsedType, parsedPriority, subject);

        if (ticket is null)
        {
            await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("Could not open ticket. Check `/ticketsetup configure` and your open-ticket limit."));
            return;
        }

        var channel = ctx.Guild.Channels.GetValueOrDefault(ticket.ChannelId);
        if (channel is not null)
        {
            var openEmbed = SectorUI.CreateBaseEmbed()
                .WithFeatureTitle("Tickets", $"Ticket #{ticket.TicketNumber}", "🎫")
                .WithColor(SectorUI.SectorPrimary)
                .WithDescription($"Creator: {ctx.User.Mention}\nType: **{ticket.Type}**\nPriority: **{ticket.Priority}**")
                .AddField("Subject", string.IsNullOrWhiteSpace(ticket.Subject) ? "No subject provided." : ticket.Subject, false);

            await channel.SendMessageAsync(new DiscordMessageBuilder().AddEmbed(openEmbed));
        }

        await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent($"Ticket created: <#{ticket.ChannelId}> (#{ticket.TicketNumber})"));
    }

    [Command("claim")]
    [RequirePermissions(DiscordPermission.ManageMessages)]
    [Description("Claim the current ticket channel")]
    public async ValueTask ClaimAsync(CommandContext ctx)
    {
        if (ctx.Guild is null)
        {
            await ctx.RespondAsync("This command can only be used in a guild.");
            return;
        }

        var claimed = await _ticketService.ClaimTicketAsync(ctx.Guild.Id, ctx.Channel.Id, ctx.User.Id);
        if (!claimed)
        {
            await ctx.RespondAsync("This channel is not an open ticket or has already been closed.");
            return;
        }

        await ctx.RespondAsync($"{ctx.User.Mention} claimed this ticket.");
    }

    [Command("close")]
    [Description("Close current ticket channel")]
    public async ValueTask CloseAsync(CommandContext ctx, [Description("Close reason")] string reason = "Resolved")
    {
        if (ctx.Guild is null)
        {
            await ctx.RespondAsync("This command can only be used in a guild.");
            return;
        }

        var db = ctx.ServiceProvider.GetRequiredService<AppDbContext>();
        var ticket = await _ticketService.GetTicketByChannelAsync(ctx.Guild.Id, ctx.Channel.Id);
        if (ticket is null)
        {
            await ctx.RespondAsync("This channel is not tracked as a ticket.");
            return;
        }

        var isCreator = ticket.CreatedByUserId == ctx.User.Id;
        var isStaff = await IsStaffAsync(db, ctx.Guild.Id, ctx.Member);
        if (!isCreator && !isStaff)
        {
            await ctx.RespondAsync("You are not allowed to close this ticket.");
            return;
        }

        var closed = await _ticketService.CloseTicketAsync(ctx.Guild.Id, ctx.Channel.Id, ctx.User.Id, reason);
        if (!closed)
        {
            await ctx.RespondAsync("Ticket is already closed.");
            return;
        }

        var transcript = await _ticketService.CreateTranscriptAsync(ctx.Channel, ctx.User.Id);

        var embed = SectorUI.CreateBaseEmbed()
            .WithFeatureTitle("Tickets", "Ticket Closed", "🔒")
            .WithColor(SectorUI.SectorWarning)
            .WithDescription($"Closed by: {ctx.User.Mention}\nReason: {reason}");

        if (transcript is not null)
        {
            embed.AddField("Transcript", transcript.FileName, false);
        }

        await ctx.RespondAsync(new DiscordMessageBuilder().AddEmbed(embed));
    }

    [Command("reopen")]
    [RequirePermissions(DiscordPermission.ManageMessages)]
    [Description("Reopen the current ticket")]
    public async ValueTask ReopenAsync(CommandContext ctx)
    {
        if (ctx.Guild is null)
        {
            await ctx.RespondAsync("This command can only be used in a guild.");
            return;
        }

        var reopened = await _ticketService.ReopenTicketAsync(ctx.Guild.Id, ctx.Channel.Id);
        if (!reopened)
        {
            await ctx.RespondAsync("This ticket is not closed or not tracked.");
            return;
        }

        await ctx.RespondAsync("Ticket reopened.");
    }

    private static bool TryParseType(string value, out TicketType ticketType)
    {
        value = value.Trim().ToLowerInvariant();
        ticketType = value switch
        {
            "support" => TicketType.Support,
            "report" => TicketType.Report,
            "bewerbung" => TicketType.Bewerbung,
            _ => TicketType.Support
        };

        return value is "support" or "report" or "bewerbung";
    }

    private static bool TryParsePriority(string value, out TicketPriority ticketPriority)
    {
        value = value.Trim().ToLowerInvariant();
        ticketPriority = value switch
        {
            "low" => TicketPriority.Low,
            "medium" => TicketPriority.Medium,
            "high" => TicketPriority.High,
            _ => TicketPriority.Medium
        };

        return value is "low" or "medium" or "high";
    }

    private static async Task<bool> IsStaffAsync(AppDbContext db, ulong guildId, DiscordMember? member)
    {
        if (member is null)
        {
            return false;
        }

        var staffRoleIds = await db.TicketStaffRoles
            .Where(x => x.GuildId == guildId)
            .Select(x => x.RoleId)
            .ToListAsync();

        if (staffRoleIds.Count == 0)
        {
            return false;
        }

        return member.Roles.Any(r => staffRoleIds.Contains(r.Id));
    }
}
