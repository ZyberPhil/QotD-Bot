using DSharpPlus;
using DSharpPlus.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QotD.Bot.Configuration;
using QotD.Bot.Data;

namespace QotD.Bot.Services;

/// <summary>
/// Background service that checks the database at the configured time each day
/// and posts the "Question of the Day" to the configured Discord channel.
/// </summary>
public sealed class QotDBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly DiscordClient _discord;
    private readonly DiscordSettings _discordSettings;
    private readonly SchedulingSettings _schedulingSettings;
    private readonly ILogger<QotDBackgroundService> _logger;

    public QotDBackgroundService(
        IServiceScopeFactory scopeFactory,
        DiscordClient discord,
        IOptions<DiscordSettings> discordSettings,
        IOptions<SchedulingSettings> schedulingSettings,
        ILogger<QotDBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _discord = discord;
        _discordSettings = discordSettings.Value;
        _schedulingSettings = schedulingSettings.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("QotD background service started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = ComputeDelayUntilNextPost();
            _logger.LogInformation("Next QotD post in {Delay:hh\\:mm\\:ss} (at {Time:HH:mm} {Tz}).",
                delay,
                DateTime.UtcNow.Add(delay),
                _schedulingSettings.Timezone);

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            await PostQuestionOfTheDayAsync(stoppingToken);
        }

        _logger.LogInformation("QotD background service stopped.");
    }

    // ──────────────────────────────────────────────────────────────────────────

    private TimeSpan ComputeDelayUntilNextPost()
    {
        var tz = _schedulingSettings.GetTimeZoneInfo();
        var postTime = _schedulingSettings.GetPostTime();

        var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
        var nextRunLocal = nowLocal.Date + postTime.ToTimeSpan();

        // If the time for today has already passed, schedule for tomorrow.
        if (nextRunLocal <= nowLocal)
            nextRunLocal = nextRunLocal.AddDays(1);

        var nextRunUtc = TimeZoneInfo.ConvertTimeToUtc(nextRunLocal, tz);
        return nextRunUtc - DateTime.UtcNow;
    }

    private async Task PostQuestionOfTheDayAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var today = DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _schedulingSettings.GetTimeZoneInfo()));

        var question = await db.Questions
            .Where(q => q.ScheduledFor == today && !q.Posted)
            .FirstOrDefaultAsync(ct);

        if (question is null)
        {
            _logger.LogWarning("No question scheduled for {Date}. Skipping post.", today);
            return;
        }

        try
        {
            var channel = await _discord.GetChannelAsync(_discordSettings.ChannelId);

            var embed = new DiscordEmbedBuilder()
                .WithTitle("❓ Question of the Day")
                .WithDescription(question.QuestionText)
                .WithColor(new DiscordColor("#5865F2"))
                .WithFooter($"Question #{question.Id} · {today:dddd, MMMM d, yyyy}")
                .WithTimestamp(DateTimeOffset.UtcNow)
                .Build();

            await channel.SendMessageAsync(new DiscordMessageBuilder().AddEmbed(embed));

            question.Posted = true;
            await db.SaveChangesAsync(ct);

            _logger.LogInformation("Posted QotD #{Id} for {Date}: \"{Text}\"",
                question.Id, today, question.QuestionText[..Math.Min(80, question.QuestionText.Length)]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to post QotD for {Date}.", today);
        }
    }
}
