using DSharpPlus;
using DSharpPlus.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using QotD.Bot.Data;
using QotD.Bot.Data.Models;
using QotD.Bot.UI;

namespace QotD.Bot.Features.Birthdays.Services;

public sealed class BirthdayBackgroundService(
    IServiceScopeFactory scopeFactory,
    IServiceProvider serviceProvider,
    ILogger<BirthdayBackgroundService> logger) : BackgroundService
{
    private DiscordClient discord => serviceProvider.GetRequiredService<DiscordClient>();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Birthday background service started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBirthdaysAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error while processing birthdays.");
            }

            // Check every hour
            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }

        logger.LogInformation("Birthday background service stopped.");
    }

    private async Task ProcessBirthdaysAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var configs = await db.BirthdayConfigs.ToListAsync(ct);
        if (configs.Count == 0) return;

        foreach (var config in configs)
        {
            try
            {
                await ProcessGuildBirthdaysAsync(db, config, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to process birthdays for Guild {GuildId}.", config.GuildId);
            }
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task ProcessGuildBirthdaysAsync(AppDbContext db, BirthdayConfig config, CancellationToken ct)
    {
        var guildConfig = await db.GuildConfigs.FirstOrDefaultAsync(c => c.GuildId == config.GuildId, ct);
        var timezone = guildConfig?.Timezone ?? "Europe/Berlin";
        var tz = TimeZoneInfo.FindSystemTimeZoneById(timezone);
        var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
        var currentDate = DateOnly.FromDateTime(nowLocal);

        if (config.LastAnnouncementDate == currentDate) return;

        var guild = await discord.GetGuildAsync(config.GuildId);
        if (guild == null) return;

        var birthdaysToday = await db.UserBirthdays
            .Where(b => b.GuildId == config.GuildId && b.Day == currentDate.Day && b.Month == currentDate.Month)
            .ToListAsync(ct);

        var todayUserIds = birthdaysToday.Select(b => b.MemberId).ToHashSet();
        
        // Final list of members who actually celebrate today
        var celebrants = new List<DiscordMember>();

        // 1. Give Roles
        foreach (var birthday in birthdaysToday)
        {
            try
            {
                var member = await guild.GetMemberAsync(birthday.MemberId);
                if (member != null)
                {
                    if (!member.Roles.Any(r => r.Id == config.BirthdayRoleId))
                    {
                        var role = guild.Roles.GetValueOrDefault(config.BirthdayRoleId);
                        if (role != null) await member.GrantRoleAsync(role, "It's their birthday!");
                    }
                    celebrants.Add(member);
                }
            }
            catch { /* Member might have left */ }
        }

        // 2. Remove Roles from anyone else who has it
        try
        {
            var role = guild.Roles.GetValueOrDefault(config.BirthdayRoleId);
            if (role != null)
            {
                // We need to fetch all members to check roles reliably, or use search
                // For simplicity in a background task, we fetch the guild members if needed
                // But DSharpPlus usually caches them if intents are enabled.
                
                var membersWithRole = guild.Members.Values
                    .Where(m => m.Roles.Any(r => r.Id == config.BirthdayRoleId))
                    .ToList();

                foreach (var member in membersWithRole)
                {
                    if (!todayUserIds.Contains(member.Id))
                    {
                        await member.RevokeRoleAsync(role, "Birthday is over.");
                    }
                }
            }
        }
        catch (Exception ex)
        {
               logger.LogWarning(ex, "Failed to cleanup birthday roles for Guild {GuildId}.", config.GuildId);
        }

        // 3. Announce
        if (celebrants.Count > 0 && config.AnnouncementChannelId > 0)
        {
            var channel = guild.Channels.GetValueOrDefault(config.AnnouncementChannelId);
            if (channel != null)
            {
                var names = string.Join(", ", celebrants.Select(m => m.Mention));
                var embed = new DiscordEmbedBuilder()
                    .WithTitle("🎂 Happy Birthday!")
                    .WithDescription($"Today we are celebrating the birthday of:\n{names}\n\nHave a fantastic day! 🎉")
                    .WithColor(CozyCoveUI.CozyGold)
                    .WithThumbnail("https://media.giphy.com/media/v1.Y2lkPTc5MGI3NjExNHJqZ3R5Z3R5Z3R5Z3R5Z3R5Z3R5Z3R5Z3R5Z3R5Z3R5JmVwPXYxX2ludGVybmFsX2dpZl9ieV9pZCZjdD1n/3o7TKDkDbIDJieKbVm/giphy.gif");

                await channel.SendMessageAsync(embed);
            }
        }

        config.LastAnnouncementDate = currentDate;
    }
}
