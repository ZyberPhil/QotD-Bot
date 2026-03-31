using DSharpPlus.Commands;
using DSharpPlus.Commands.ContextChecks;
using DSharpPlus.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QotD.Bot.Data;
using QotD.Bot.Features.Logging.Models;
using System.Text;

namespace QotD.Bot.Features.Logging.Commands;

public sealed class LogSetupCommand
{
    [Command("logsetup")]
    [RequirePermissions(DiscordPermission.ManageGuild)]
    [System.ComponentModel.Description("Open the interactive logging configuration panel.")]
    public async ValueTask ExecuteAsync(CommandContext ctx)
    {
        await ctx.DeferResponseAsync();
        
        var db = ctx.ServiceProvider.GetRequiredService<AppDbContext>();

        var configs = await db.LogRoutingConfigs
            .Where(c => c.GuildId == ctx.Guild!.Id)
            .ToListAsync();

        var embed = new DiscordEmbedBuilder()
            .WithTitle("⚙️ Logging Configuration Panel")
            .WithColor(DiscordColor.Blurple)
            .WithDescription("Select a Log Type below to assign or change its destination channel, or select multiple types to route them to the same channel.");

        var sb = new StringBuilder();
        sb.AppendLine("Current Configuration:");
        foreach (LogType type in Enum.GetValues(typeof(LogType)))
        {
            var cfg = configs.FirstOrDefault(c => c.LogType == type);
            if (cfg != null && cfg.IsEnabled && cfg.ChannelId > 0)
            {
                sb.AppendLine($"- **{type}** ➔ <#{cfg.ChannelId}>");
            }
            else
            {
                sb.AppendLine($"- **{type}** ➔ ❌ Not Configured");
            }
        }
        embed.AddField("Mappings", sb.ToString());

        var typeOptions = Enum.GetValues(typeof(LogType))
            .Cast<LogType>()
            .Select(t => new DiscordSelectComponentOption(t.ToString(), t.ToString(), $"Configure destination for {t} logs"))
            .ToList();

        var typeSelect = new DiscordSelectComponent("logsetup_typeselect", "Select a Log Type to edit...", typeOptions);
        var btnClose = new DiscordButtonComponent(DiscordButtonStyle.Danger, "logsetup_close", "Close Panel");

        await ctx.EditResponseAsync(new DiscordWebhookBuilder()
            .AddEmbed(embed)
            .AddActionRowComponent(new DiscordActionRowComponent(new DiscordComponent[] { typeSelect }))
            .AddActionRowComponent(new DiscordActionRowComponent(new DiscordComponent[] { btnClose })));
    }
}
