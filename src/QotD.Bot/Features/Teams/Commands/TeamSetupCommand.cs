using DSharpPlus;
using DSharpPlus.Commands;
using DSharpPlus.Commands.ContextChecks;
using DSharpPlus.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QotD.Bot.Data;
using QotD.Bot.UI;
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
            .WithFeatureTitle("Teams", "Dynamic List Setup", "📋")
            .WithColor(SectorUI.SectorPrimary)
            .WithDescription("Configure the dynamic team list message. The bot automatically updates the message in the selected channel when tracked roles change.");

        var sb = new StringBuilder();
        sb.AppendLine($"**Target Channel:** {(config?.ChannelId > 0 ? $"<#{config.ChannelId}>" : "❌ Not configured")}");
        
        var rolesStr = config?.TrackedRoles?.Length > 0 
            ? string.Join(", ", config.TrackedRoles.Select(r => $"<@&{r}>")) 
            : "❌ Not configured";
        sb.AppendLine($"**Tracked Roles:** {rolesStr}");
        
        sb.AppendLine($"**Title:** {config?.CustomTitle ?? "Default (📋 Team List)"}");
        
        var templateDisplay = string.IsNullOrWhiteSpace(config?.CustomTemplate) 
            ? "Using default template" 
            : "Custom template enabled";
        sb.AppendLine($"**Template:** {templateDisplay}");
        sb.AppendLine("**Template Tokens:** `{role_name}`, `{role_mention}`, `{member_count}`, `{members_list}`");
        sb.AppendLine($"**Footer:** {(string.IsNullOrWhiteSpace(config?.CustomFooter) ? "❌ Not configured" : "✅ Configured")}");
        
        embed.AddField("Current Setup", sb.ToString());

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
