using DSharpPlus.Commands;
using DSharpPlus.Entities;
using Microsoft.EntityFrameworkCore;
using QotD.Bot.Data;
using QotD.Bot.Data.Models;
using System.ComponentModel;
using DescriptionAttribute = System.ComponentModel.DescriptionAttribute;

namespace QotD.Bot.Features.Birthdays.Commands;

/// <summary>
/// Subcommand group: /birthday set, /birthday remove
/// </summary>
[DSharpPlus.Commands.Command("birthday")]
[Description("Manage your birthday reminder")]
public sealed class BirthdayCommands(AppDbContext db)
{
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
}
