using DSharpPlus;
using DSharpPlus.EventArgs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using QotD.Bot.Data;

namespace QotD.Bot.Features.Teams.Services;

public sealed class TeamListEventHandler :
    IEventHandler<GuildMemberUpdatedEventArgs>,
    IEventHandler<GuildMemberRemovedEventArgs>
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TeamListService _teamListService;
    private readonly TeamActivityService _teamActivityService;
    private readonly ILogger<TeamListEventHandler> _logger;

    public TeamListEventHandler(IServiceScopeFactory scopeFactory, TeamListService teamListService, TeamActivityService teamActivityService, ILogger<TeamListEventHandler> logger)
    {
        _scopeFactory = scopeFactory;
        _teamListService = teamListService;
        _teamActivityService = teamActivityService;
        _logger = logger;
    }

    public async Task HandleEventAsync(DiscordClient client, GuildMemberUpdatedEventArgs e)
    {
        // Check if roles have changed
        var oldRoles = e.RolesBefore.Select(r => r.Id).ToList();
        var newRoles = e.RolesAfter.Select(r => r.Id).ToList();

        // If no roles changed, we don't care
        if (oldRoles.SequenceEqual(newRoles)) return;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var config = await db.TeamListConfigs.FirstOrDefaultAsync(c => c.GuildId == e.Guild.Id);
        if (config == null || config.TrackedRoles.Length == 0) return;

        // Check if any tracked role was added or removed
        bool requiresRefresh = config.TrackedRoles.Any(tr => 
            (oldRoles.Contains(tr) && !newRoles.Contains(tr)) ||
            (!oldRoles.Contains(tr) && newRoles.Contains(tr)));

        if (requiresRefresh)
        {
            var oldRoleId = config.TrackedRoles.FirstOrDefault(oldRoles.Contains);
            var newRoleId = config.TrackedRoles.FirstOrDefault(newRoles.Contains);

            if (oldRoleId != newRoleId)
            {
                var oldRoleName = oldRoleId != 0 ? e.Guild.Roles.GetValueOrDefault(oldRoleId)?.Name : null;
                var newRoleName = newRoleId != 0 ? e.Guild.Roles.GetValueOrDefault(newRoleId)?.Name : null;

                await _teamActivityService.RecordRoleChangeAsync(
                    e.Guild.Id,
                    e.Member.Id,
                    oldRoleId == 0 ? null : oldRoleId,
                    oldRoleName,
                    newRoleId == 0 ? null : newRoleId,
                    newRoleName,
                    DateTimeOffset.UtcNow,
                    "Tracked team role changed");
            }

            await _teamListService.RefreshTeamListAsync(client, e.Guild.Id);
        }
    }

    public async Task HandleEventAsync(DiscordClient client, GuildMemberRemovedEventArgs e)
    {
        // User left the guild, they might have had a tracked role
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var config = await db.TeamListConfigs.FirstOrDefaultAsync(c => c.GuildId == e.Guild.Id);
        if (config == null || config.TrackedRoles.Length == 0) return;

        // We could just blindly refresh since they left, to be safe.
        // If we want to optimize, we'd check if they had the role, but their roles aren't cached reliably after they leave unless member intents cache it.
        // It's safer to just refresh the list.
        await _teamListService.RefreshTeamListAsync(client, e.Guild.Id);
    }
}
