using DSharpPlus;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using QotD.Bot.Data;

namespace QotD.Bot.Features.Teams.Services;

public sealed class TeamActivityBackgroundService(
    IServiceProvider serviceProvider,
    IServiceScopeFactory scopeFactory,
    ILogger<TeamActivityBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromHours(24);
    private DiscordClient Discord => serviceProvider.GetRequiredService<DiscordClient>();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Team activity background service started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error while running team activity weekly checks.");
            }

            await Task.Delay(TickInterval, stoppingToken);
        }

        logger.LogInformation("Team activity background service stopped.");
    }

    private async Task ProcessAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<TeamActivityService>();

        var guildIds = await db.TeamListConfigs
            .AsNoTracking()
            .Where(x => x.TrackedRoles.Length > 0)
            .Select(x => x.GuildId)
            .ToListAsync(ct);

        foreach (var guildId in guildIds)
        {
            if (ct.IsCancellationRequested)
            {
                return;
            }

            try
            {
                await service.EvaluatePreviousWeekAndWarnAsync(Discord, guildId, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed team activity check for guild {GuildId}", guildId);
            }
        }
    }
}