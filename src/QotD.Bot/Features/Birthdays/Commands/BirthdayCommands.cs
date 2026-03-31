using DSharpPlus;
using DSharpPlus.Commands;
using DSharpPlus.Commands.ContextChecks;
using DSharpPlus.Entities;
using Microsoft.EntityFrameworkCore;
using QotD.Bot.Data;
using QotD.Bot.Data.Models;
using System.ComponentModel;
using DescriptionAttribute = System.ComponentModel.DescriptionAttribute;

namespace QotD.Bot.Features.Birthdays.Commands;

public sealed class BirthdayCommands(AppDbContext db)
{
    [DSharpPlus.Commands.Command("birthday")]
    [Description("Manage your birthday reminder")]
    public async Task BirthdayAsync(CommandContext ctx)
    {
        // Subcommand placeholder if needed, but we'll use top-level or child commands
    }

    [DSharpPlus.Commands.Command("set")]
    [Description("Set your birthday (Day and Month)")]
    public async Task SetBirthdayAsync(CommandContext ctx, 
        [Description("Day (1-31)")] int day, 
        [Description("Month (1-12)")] int month)
    {
        if (day < 1 || day > 31 || month < 1 || month > 12)
        {
            await ctx.RespondAsync("❌ Invalid date. Please use valid day (1-31) and month (1-12).");
            return;
        }

        // Basic check for month lengths
        if ((month == 4 || month == 6 || month == 9 || month == 11) && day > 30)
        {
            await ctx.RespondAsync("❌ This month only has 30 days.");
            return;
        }
        if (month == 2 && day > 29)
        {
            await ctx.RespondAsync("❌ February only has up to 29 days.");
            return;
        }

        var guildId = ctx.Guild!.Id;
        var userId = ctx.User.Id;

        var birthday = await db.UserBirthdays.FirstOrDefaultAsync(b => b.GuildId == guildId && b.MemberId == userId);
        if (birthday == null)
        {
            birthday = new UserBirthday { GuildId = guildId, MemberId = userId };
            db.UserBirthdays.Add(birthday);
        }

        birthday.Day = day;
        birthday.Month = month;
        await db.SaveChangesAsync();

        await ctx.RespondAsync($"✅ Your birthday has been set to **{day:D2}.{month:D2}**!");
    }

    [DSharpPlus.Commands.Command("remove")]
    [Description("Remove your birthday reminder")]
    public async Task RemoveBirthdayAsync(CommandContext ctx)
    {
        var guildId = ctx.Guild!.Id;
        var userId = ctx.User.Id;

        var birthday = await db.UserBirthdays.FirstOrDefaultAsync(b => b.GuildId == guildId && b.MemberId == userId);
        if (birthday == null)
        {
            await ctx.RespondAsync("❌ You don't have a birthday set.");
            return;
        }

        db.UserBirthdays.Remove(birthday);
        await db.SaveChangesAsync();

        await ctx.RespondAsync("✅ Your birthday reminder has been removed.");
    }

    [DSharpPlus.Commands.Command("birthdaysetup")]
    [Description("Configure the birthday reminder system")]
    [RequirePermissions(DiscordPermission.ManageGuild)]
    public async Task SetupAsync(CommandContext ctx, 
        [Description("Channel for birthday announcements")] DiscordChannel channel, 
        [Description("Role to give on birthdays")] DiscordRole role)
    {
        var guildId = ctx.Guild!.Id;

        var config = await db.BirthdayConfigs.FirstOrDefaultAsync(c => c.GuildId == guildId);
        if (config == null)
        {
            config = new BirthdayConfig { GuildId = guildId };
            db.BirthdayConfigs.Add(config);
        }

        config.AnnouncementChannelId = channel.Id;
        config.BirthdayRoleId = role.Id;
        await db.SaveChangesAsync();

        await ctx.RespondAsync($"✅ Birthday reminder configured!\n" +
                               $"- Announcements: {channel.Mention}\n" +
                               $"- Role: {role.Mention}");
    }
}
