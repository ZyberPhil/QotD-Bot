using DSharpPlus.Commands;
using DSharpPlus.Commands.ContextChecks;
using DSharpPlus.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QotD.Bot.Data;
using QotD.Bot.Features.Logging.Models;
using QotD.Bot.UI;
using System.Text;

namespace QotD.Bot.Features.Logging.Commands;

public sealed class LogSetupCommand
{
    private static readonly LogType[] ConfigurableLogTypes =
    [
        LogType.MessageDeleted,
        LogType.MessageUpdated,
        LogType.MemberJoinLeave,
        LogType.VoiceJoinLeave,
        LogType.BotAction,
        LogType.BotError
    ];

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

        var embed = SectorUI.CreateBaseEmbed()
            .WithFeatureTitle("Logging", "Configuration Panel", "⚙️")
            .WithColor(SectorUI.SectorPrimary)
            .WithDescription("Choose one or more log types below to assign or change their destination channel.");

        var sb = new StringBuilder();
        sb.AppendLine("Current Routing:");
        foreach (var type in ConfigurableLogTypes)
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

        var typeOptions = ConfigurableLogTypes
            .Select(t => new DiscordSelectComponentOption(t.ToString(), t.ToString(), $"Configure destination for {t} logs"))
            .ToList();

        var typeSelect = new DiscordSelectComponent("logsetup_typeselect", "Select a log type...", typeOptions);
        var btnClose = new DiscordButtonComponent(DiscordButtonStyle.Danger, "logsetup_close", "Close Panel");

        await ctx.EditResponseAsync(new DiscordWebhookBuilder()
            .AddEmbed(embed)
            .AddActionRowComponent(new DiscordActionRowComponent(new DiscordComponent[] { typeSelect }))
            .AddActionRowComponent(new DiscordActionRowComponent(new DiscordComponent[] { btnClose })));
    }
}
