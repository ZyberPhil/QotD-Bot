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

[Command("ticketsetup")]
public sealed class TicketSetupCommand
{
    private readonly TicketService _ticketService;

    public TicketSetupCommand(TicketService ticketService)
    {
        _ticketService = ticketService;
    }

    [Command("status")]
    [RequirePermissions(DiscordPermission.ManageGuild)]
    [System.ComponentModel.Description("Shows current ticket configuration.")]
    public async ValueTask StatusAsync(CommandContext ctx)
    {
        if (ctx.Guild is null)
        {
            await ctx.RespondAsync("This command can only be used in a guild.");
            return;
        }

        var db = ctx.ServiceProvider.GetRequiredService<AppDbContext>();
        var config = await db.TicketConfigs
            .Include(x => x.StaffRoles)
            .FirstOrDefaultAsync(x => x.GuildId == ctx.Guild.Id);

        var embed = SectorUI.CreateBaseEmbed()
            .WithFeatureTitle("Tickets", "Setup Status", "🎫")
            .WithColor(SectorUI.SectorPrimary);

        if (config is null)
        {
            embed.WithDescription("No ticket setup configured yet. Use `/ticketsetup configure`.");
            await ctx.RespondAsync(new DiscordMessageBuilder().AddEmbed(embed));
            return;
        }

        var roleText = config.StaffRoles.Count == 0
            ? "❌ No staff role configured"
            : string.Join(", ", config.StaffRoles.Select(r => $"<@&{r.RoleId}>"));

        embed.WithDescription(
            $"Enabled: **{config.IsEnabled}**\n" +
            $"Category: <#{config.CategoryId}>\n" +
            $"Staff roles: {roleText}\n" +
            $"Max open per user: **{config.MaxOpenTicketsPerUser}**\n" +
            $"Default SLA: **{config.DefaultSlaMinutes} min**");

        await ctx.RespondAsync(new DiscordMessageBuilder().AddEmbed(embed));
    }

    [Command("configure")]
    [RequirePermissions(DiscordPermission.ManageGuild)]
    [System.ComponentModel.Description("Configure ticket category, one staff role, limits, and default SLA.")]
    public async ValueTask ConfigureAsync(
        CommandContext ctx,
        [System.ComponentModel.Description("Category where ticket channels are created")] DiscordChannel category,
        [System.ComponentModel.Description("Support role")] DiscordRole staffRole,
        [System.ComponentModel.Description("Max open tickets per user (1-10)")] int maxOpenPerUser = 2,
        [System.ComponentModel.Description("Default SLA in minutes (15-1440)")] int slaMinutes = 180)
    {
        if (ctx.Guild is null)
        {
            await ctx.RespondAsync("This command can only be used in a guild.");
            return;
        }

        if (category.Type != DiscordChannelType.Category)
        {
            await ctx.RespondAsync("Please provide a category channel.");
            return;
        }

        await _ticketService.UpsertConfigAsync(ctx.Guild.Id, category.Id, [staffRole.Id], maxOpenPerUser, slaMinutes);

        var embed = SectorUI.CreateBaseEmbed()
            .WithFeatureTitle("Tickets", "Configuration Updated", "🎫")
            .WithColor(SectorUI.SectorSuccessGreen)
            .WithDescription(
                $"Category: {category.Mention}\n" +
                $"Staff role: {staffRole.Mention}\n" +
                $"Max open per user: **{Math.Clamp(maxOpenPerUser, 1, 10)}**\n" +
                $"Default SLA: **{Math.Clamp(slaMinutes, 15, 1440)} min**");

        await ctx.RespondAsync(new DiscordMessageBuilder().AddEmbed(embed));
    }

    [Command("setlog")]
    [RequirePermissions(DiscordPermission.ManageGuild)]
    [System.ComponentModel.Description("Configure separate ticket log channel routing.")]
    public async ValueTask SetLogAsync(
        CommandContext ctx,
        [System.ComponentModel.Description("Event type: created, claimed, closed, reopened, escalated")] string eventType,
        [System.ComponentModel.Description("Target log channel")] DiscordChannel channel)
    {
        if (ctx.Guild is null)
        {
            await ctx.RespondAsync("This command can only be used in a guild.");
            return;
        }

        if (!TryParseEventType(eventType, out var parsed))
        {
            await ctx.RespondAsync("Unknown event type. Use one of: created, claimed, closed, reopened, escalated.");
            return;
        }

        var db = ctx.ServiceProvider.GetRequiredService<AppDbContext>();
        var existing = await db.TicketLogConfigs.FirstOrDefaultAsync(x => x.GuildId == ctx.Guild.Id && x.EventType == parsed);

        if (existing is null)
        {
            db.TicketLogConfigs.Add(new TicketLogConfig
            {
                GuildId = ctx.Guild.Id,
                EventType = parsed,
                ChannelId = channel.Id,
                IsEnabled = true,
                CreatedAtUtc = DateTimeOffset.UtcNow
            });
        }
        else
        {
            existing.ChannelId = channel.Id;
            existing.IsEnabled = true;
        }

        await db.SaveChangesAsync();
        await ctx.RespondAsync($"Ticket log routing for **{parsed}** set to {channel.Mention}.");
    }

    private static bool TryParseEventType(string value, out TicketLogEventType eventType)
    {
        value = value.Trim().ToLowerInvariant();
        eventType = value switch
        {
            "created" => TicketLogEventType.Created,
            "claimed" => TicketLogEventType.Claimed,
            "closed" => TicketLogEventType.Closed,
            "reopened" => TicketLogEventType.Reopened,
            "escalated" => TicketLogEventType.Escalated,
            _ => TicketLogEventType.Created
        };

        return value is "created" or "claimed" or "closed" or "reopened" or "escalated";
    }
}
