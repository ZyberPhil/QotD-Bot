using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace QotD.Bot.Features.MiniGames.Services;

public sealed class BlackjackCleanupService(
    BlackjackService blackjackService,
    MiniGamesEventHandler eventHandler,
    ILogger<BlackjackCleanupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Mini-game cleanup service started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                blackjackService.CleanupStaleGames(TimeSpan.FromMinutes(10));
                eventHandler.CleanupUnusedLocks();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during mini-game resource cleanup.");
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}
