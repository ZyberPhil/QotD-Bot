using DSharpPlus;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using QotD.Bot.Data;
using QotD.Bot.Data.Models;

namespace QotD.Bot.Features.QotD.Services;

/// <summary>
/// Background service that checks every minute whether any guild is due for
/// their Question of the Day and delegates the actual posting to
/// <see cref="QotDPostingService"/>.
/// </summary>
public sealed class QotDBackgroundService(
    IServiceScopeFactory scopeFactory,
    IServiceProvider serviceProvider,
    QotDPostingService postingService,
    ILogger<QotDBackgroundService> logger) : BackgroundService
{
    private DiscordClient discord => serviceProvider.GetRequiredService<DiscordClient>();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("QotD background service started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessGuildsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error while processing guild QotD schedules.");
            }

            // Sleep until the next full minute
            var now = DateTime.UtcNow;
            var nextMinute = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0)
                .AddMinutes(1);
            await Task.Delay(nextMinute - now, stoppingToken);
        }

        logger.LogInformation("QotD background service stopped.");
    }

    private async Task ProcessGuildsAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var configs = await db.GuildConfigs.AsNoTracking().ToListAsync(ct);
        if (configs.Count == 0) return;

        await Parallel.ForEachAsync(configs, ct, async (config, token) =>
        {
            if (config.ChannelId == 0) return;

            var tz = TimeZoneInfo.FindSystemTimeZoneById(config.Timezone);
            var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);

            if (nowLocal.Hour == config.PostTime.Hour && nowLocal.Minute == config.PostTime.Minute)
            {
                // We need a fresh scope/DB context for each parallel execution to avoid threading issues
                using var innerScope = scopeFactory.CreateScope();
                var innerDb = innerScope.ServiceProvider.GetRequiredService<AppDbContext>();
                await TryPostForGuildAsync(innerDb, config, nowLocal.Date, token);
            }
        });
    }

    private async Task TryPostForGuildAsync(
        AppDbContext db, GuildConfig config, DateTime localDate, CancellationToken ct)
    {
        var dateOnly = DateOnly.FromDateTime(localDate);

        var question = await db.Questions.AsNoTracking()
            .FirstOrDefaultAsync(q => q.ScheduledFor == dateOnly, ct);

        if (question == null)
        {
            logger.LogWarning("No question scheduled for {Date} (Guild {GuildId}).",
                dateOnly, config.GuildId);
            return;
        }

        var alreadyPosted = await db.GuildHistories.AsNoTracking()
            .AnyAsync(h => h.GuildId == config.GuildId && h.QuestionId == question.Id, ct);
        if (alreadyPosted) return;

        try
        {
            logger.LogInformation("Posting QotD to Guild {GuildId}, Channel {ChannelId}...",
                config.GuildId, config.ChannelId);

            var channel = await discord.GetChannelAsync(config.ChannelId);
            await postingService.PostQuestionAsync(
                channel, question.QuestionText, question.Id.ToString(), config, dateOnly);

            question.Posted = true;
            db.GuildHistories.Add(new GuildHistory
            {
                GuildId = config.GuildId,
                QuestionId = question.Id
            });
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to post QotD for Guild {GuildId}.", config.GuildId);
        }
    }
}
