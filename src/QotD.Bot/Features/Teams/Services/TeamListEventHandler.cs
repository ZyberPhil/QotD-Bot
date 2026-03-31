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
    private readonly IServiceProvider _serviceProvider;
    private readonly TeamListService _teamListService;
    private readonly ILogger<TeamListEventHandler> _logger;

    public TeamListEventHandler(IServiceProvider serviceProvider, TeamListService teamListService, ILogger<TeamListEventHandler> logger)
    {
        _serviceProvider = serviceProvider;
        _teamListService = teamListService;
        _logger = logger;
    }

    public async Task HandleEventAsync(DiscordClient client, GuildMemberUpdatedEventArgs e)
    {
        // Check if roles have changed
        var oldRoles = e.RolesBefore.Select(r => r.Id).ToList();
        var newRoles = e.RolesAfter.Select(r => r.Id).ToList();

        // If no roles changed, we don't care
        if (oldRoles.SequenceEqual(newRoles)) return;

        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var config = await db.TeamListConfigs.FirstOrDefaultAsync(c => c.GuildId == e.Guild.Id);
        if (config == null || config.TrackedRoles.Length == 0) return;

        // Check if any tracked role was added or removed
        bool requiresRefresh = config.TrackedRoles.Any(tr => 
            (oldRoles.Contains(tr) && !newRoles.Contains(tr)) ||
            (!oldRoles.Contains(tr) && newRoles.Contains(tr)));

        if (requiresRefresh)
        {
            await _teamListService.RefreshTeamListAsync(client, e.Guild.Id);
        }
    }

    public async Task HandleEventAsync(DiscordClient client, GuildMemberRemovedEventArgs e)
    {
        // User left the guild, they might have had a tracked role
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var config = await db.TeamListConfigs.FirstOrDefaultAsync(c => c.GuildId == e.Guild.Id);
        if (config == null || config.TrackedRoles.Length == 0) return;

        // We could just blindly refresh since they left, to be safe.
        // If we want to optimize, we'd check if they had the role, but their roles aren't cached reliably after they leave unless member intents cache it.
        // It's safer to just refresh the list.
        await _teamListService.RefreshTeamListAsync(client, e.Guild.Id);
    }
}
