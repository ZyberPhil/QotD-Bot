using DSharpPlus;
using DSharpPlus.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using QotD.Bot.Data;
using QotD.Bot.Data.Models;

namespace QotD.Bot.Services;

/// <summary>
/// Background service that checks the database every minute to see if any guilds
/// are due for their "Question of the Day".
/// </summary>
public sealed class QotDBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly DiscordClient _discord;
    private readonly ILogger<QotDBackgroundService> _logger;

    public QotDBackgroundService(
        IServiceScopeFactory scopeFactory,
        DiscordClient discord,
        ILogger<QotDBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _discord = discord;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("QotD background service (Multi-Guild) started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessGuildsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while processing guild QotD schedules.");
            }

            // Sleep until the next full minute
            var now = DateTime.UtcNow;
            var nextMinute = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0).AddMinutes(1);
            var delay = nextMinute - now;

            await Task.Delay(delay, stoppingToken);
        }

        _logger.LogInformation("QotD background service stopped.");
    }

    private async Task ProcessGuildsAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var configs = await db.GuildConfigs.ToListAsync(ct);
        if (configs.Count == 0) return;

        foreach (var config in configs)
        {
            if (config.ChannelId == 0) continue;

            var tz = TimeZoneInfo.FindSystemTimeZoneById(config.Timezone);
            var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
            
            // Check if we are within the current minute of the scheduled post time
            if (nowLocal.Hour == config.PostTime.Hour && nowLocal.Minute == config.PostTime.Minute)
            {
                await TryPostForGuildAsync(db, config, nowLocal.Date, ct);
            }
        }
    }

    private async Task TryPostForGuildAsync(AppDbContext db, GuildConfig config, DateTime localDate, CancellationToken ct)
    {
        var dateOnly = DateOnly.FromDateTime(localDate);

        // 1. Get today's question
        var question = await db.Questions
            .AsNoTracking()
            .FirstOrDefaultAsync(q => q.ScheduledFor == dateOnly, ct);

        if (question == null)
        {
             _logger.LogWarning("No question scheduled for {Date} (Guild {GuildId}).", dateOnly, config.GuildId);
             return;
        }

        // 2. Check if already posted to this guild
        var alreadyPosted = await db.GuildHistories.AnyAsync(h => h.GuildId == config.GuildId && h.QuestionId == question.Id, ct);
        if (alreadyPosted) return;

        try
        {
            _logger.LogInformation("Posting QotD to Guild {GuildId}, Channel {ChannelId}...", config.GuildId, config.ChannelId);
            
            var channel = await _discord.GetChannelAsync(config.ChannelId);
            var embed = new DiscordEmbedBuilder()
                .WithTitle("❓ Question of the Day")
                .WithDescription(question.QuestionText)
                .WithColor(new DiscordColor("#5865F2"))
                .WithFooter($"Question #{question.Id} · {dateOnly:dddd, MMMM d, yyyy}")
                .WithTimestamp(DateTimeOffset.UtcNow)
                .Build();

            await channel.SendMessageAsync(new DiscordMessageBuilder().AddEmbed(embed));

            // Record in history
            db.GuildHistories.Add(new GuildHistory 
            { 
                GuildId = config.GuildId, 
                QuestionId = question.Id 
            });
            
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to post QotD for Guild {GuildId}.", config.GuildId);
        }
    }
}
