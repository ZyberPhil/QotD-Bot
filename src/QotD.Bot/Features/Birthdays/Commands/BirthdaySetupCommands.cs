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

/// <summary>
/// Top-level admin command: /birthdaysetup
/// </summary>
public sealed class BirthdaySetupCommands(AppDbContext db)
{
    [DSharpPlus.Commands.Command("birthdaysetup")]
    [Description("Configure the birthday reminder system (Admin only)")]
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
