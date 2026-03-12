using DSharpPlus;
using DSharpPlus.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using QotD.Bot.Data;
using QotD.Bot.Data.Models;
using QotD.Bot.Services;
using QotD.Bot.UI;


namespace QotD.Bot.Services;

/// <summary>
/// Background service that checks the database every minute to see if any guilds
/// are due for their "Question of the Day".
/// </summary>
public sealed class QotDBackgroundService(
    IServiceScopeFactory scopeFactory,
    DiscordClient discord,
    ILogger<QotDBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("QotD background service (Multi-Guild) started.");

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
            var nextMinute = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0).AddMinutes(1);
            var delay = nextMinute - now;

            await Task.Delay(delay, stoppingToken);
        }

        logger.LogInformation("QotD background service stopped.");
    }

    private async Task ProcessGuildsAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
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
             logger.LogWarning("No question scheduled for {Date} (Guild {GuildId}).", dateOnly, config.GuildId);
             return;
        }

        // 2. Check if already posted to this guild
        var alreadyPosted = await db.GuildHistories.AnyAsync(h => h.GuildId == config.GuildId && h.QuestionId == question.Id, ct);
        if (alreadyPosted) return;

        try
        {
            logger.LogInformation("Posting QotD to Guild {GuildId}, Channel {ChannelId}...", config.GuildId, config.ChannelId);
            
            var channel = await discord.GetChannelAsync(config.ChannelId);
            DiscordMessage? message;

            DiscordEmbedBuilder embedBuilder;

            if (!string.IsNullOrWhiteSpace(config.MessageTemplate))
            {
                var formattedDescription = config.MessageTemplate
                    .Replace("{message}", question.QuestionText)
                    .Replace("{date}", dateOnly.ToString("dd.MM.yyyy"))
                    .Replace("{id}", question.Id.ToString());

                embedBuilder = CozyCoveUI.CreateBaseEmbed("❓ Frage des Tages", formattedDescription);
            }
            else
            {
                embedBuilder = CozyCoveUI.CreateBaseEmbed("❓ Frage des Tages", question.QuestionText);
                embedBuilder.AddField("Diskussion", "*Gerne kannst du deine Gedanken im Thread unten teilen!*");
            }

            embedBuilder.WithFooter($"Beitrag #{question.Id} · {dateOnly:dddd, dd. MMMM yyyy}", CozyCoveUI.COZY_ICON_URL)
                        .WithTimestamp(DateTimeOffset.UtcNow);

            message = await channel.SendMessageAsync(new DiscordMessageBuilder()
                .WithContent("> 🧵 **Die Antworten findet ihr im Thread unter dieser Nachricht!**")
                .AddEmbed(embedBuilder.Build()));

            // 3. Create Thread immediately after sending
            await TryCreateThreadAsync(channel, message, dateOnly);

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
            logger.LogError(ex, "Failed to post QotD for Guild {GuildId}.", config.GuildId);
        }
    }

    private async Task TryCreateThreadAsync(DiscordChannel channel, DiscordMessage? message, DateOnly date)
    {
        if (message == null) return;

        try
        {
            // Permission Check: Ensure the bot has permission to create public threads.
            // DSharpPlus v5 uses robust permission checks via the channel's effective permissions.
            var currentMember = await channel.Guild.GetMemberAsync(discord.CurrentUser.Id);
            var permissions = channel.PermissionsFor(currentMember);

            if (!permissions.HasPermission(DiscordPermission.CreatePublicThreads))
            {
                logger.LogWarning("Missing 'CreatePublicThreads' permission in channel '{ChannelName}' ({ChannelId}) on Guild {GuildId}.", 
                    channel.Name, channel.Id, channel.Guild.Id);
                return;
            }

            // naming the thread dynamically including the date for context.
            var threadName = $"QotD Answers - {date:dd.MM.yyyy}";
            
            // Sidebar-Optimierung (UX): AutoArchiveDuration.Hour (60 minutes).
            // DSharpPlus v5: message.CreateThreadAsync(string name, AutoArchiveDuration archiveDuration)
            // This is crucial for 'Sidebar Hygiene' in Discord. 
            // By setting the archive duration to 1 hour, inactive QotD threads are 
            // archived quickly, preventing the channel's sidebar from becoming cluttered 
            // with dozens of old discussion threads while still being accessible to those 
            // actively engaging.
            await message.CreateThreadAsync(threadName, DiscordAutoArchiveDuration.Hour);
            
            logger.LogInformation("Thread '{ThreadName}' successfully created for QotD in channel {ChannelId}.", 
                threadName, channel.Id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create thread for QotD message {MessageId} in channel {ChannelId}.", 
                message.Id, channel.Id);
        }
    }
}

