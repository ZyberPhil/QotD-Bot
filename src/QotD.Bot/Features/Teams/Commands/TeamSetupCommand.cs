using DSharpPlus;
using DSharpPlus.Commands;
using DSharpPlus.Commands.ContextChecks;
using DSharpPlus.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QotD.Bot.Data;
using System.Text;

namespace QotD.Bot.Features.Teams.Commands;

public sealed class TeamSetupCommand
{
    [Command("teamsetup")]
    [RequirePermissions(DiscordPermission.ManageGuild)]
    [System.ComponentModel.Description("Open the dynamic team list configuration panel.")]
    public async ValueTask ExecuteAsync(CommandContext ctx)
    {
        await ctx.DeferResponseAsync();

        var db = ctx.ServiceProvider.GetRequiredService<AppDbContext>();
        var config = await db.TeamListConfigs.FirstOrDefaultAsync(c => c.GuildId == ctx.Guild!.Id);

        var embed = new DiscordEmbedBuilder()
            .WithTitle("📋 Dynamic Team List Setup")
            .WithColor(DiscordColor.Blurple)
            .WithDescription("Configure the dynamic team list message. The bot will automatically update the message in the selected channel whenever members gain or lose the tracked roles.");

        var sb = new StringBuilder();
        sb.AppendLine($"**Target Channel:** {(config?.ChannelId > 0 ? $"<#{config.ChannelId}>" : "❌ Not Set")}");
        
        var rolesStr = config?.TrackedRoles?.Length > 0 
            ? string.Join(", ", config.TrackedRoles.Select(r => $"<@&{r}>")) 
            : "❌ None";
        sb.AppendLine($"**Tracked Roles:** {rolesStr}");
        
        sb.AppendLine($"**Title:** {config?.CustomTitle ?? "Default (📋 Team List)"}");
        
        var templateDisplay = string.IsNullOrWhiteSpace(config?.CustomTemplate) 
            ? "Default Template" 
            : "Custom Template Active";
        sb.AppendLine($"**Template:** {templateDisplay}");
        sb.AppendLine($"**Footer:** {(string.IsNullOrWhiteSpace(config?.CustomFooter) ? "❌ Not Set" : "✅ Set")}");
        
        embed.AddField("Current Configuration", sb.ToString());

        var channelSelect = new DiscordChannelSelectComponent("teamsetup_channel", "Select target channel...");
        var roleSelect = new DiscordRoleSelectComponent("teamsetup_roles", "Select roles to track...", minOptions: 1, maxOptions: 10);
        var btnCustomText = new DiscordButtonComponent(DiscordButtonStyle.Primary, "teamsetup_custom_text", "Customize Text (Header/Footer)");
        var btnRefresh = new DiscordButtonComponent(DiscordButtonStyle.Success, "teamsetup_refresh", "Force Refresh List");

        await ctx.EditResponseAsync(new DiscordWebhookBuilder()
            .AddEmbed(embed)
            .AddActionRowComponent(new DiscordActionRowComponent(new DiscordComponent[] { channelSelect }))
            .AddActionRowComponent(new DiscordActionRowComponent(new DiscordComponent[] { roleSelect }))
            .AddActionRowComponent(new DiscordActionRowComponent(new DiscordComponent[] { btnCustomText, btnRefresh })));
    }
}
