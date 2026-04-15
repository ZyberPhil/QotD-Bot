using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using DSharpPlus.Interactivity;
using DSharpPlus.Interactivity.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QotD.Bot.Data;
using QotD.Bot.Data.Models;
using QotD.Bot.UI;
using System.Text;

namespace QotD.Bot.Features.Teams.Services;

public sealed class TeamSetupEventHandler : 
    IEventHandler<ComponentInteractionCreatedEventArgs>
{
    private readonly IServiceProvider _serviceProvider;
    private readonly TeamListService _teamListService;

    public TeamSetupEventHandler(IServiceProvider serviceProvider, TeamListService teamListService)
    {
        _serviceProvider = serviceProvider;
        _teamListService = teamListService;
    }

    public async Task HandleEventAsync(DiscordClient client, ComponentInteractionCreatedEventArgs e)
    {
        if (e.Interaction.Data.CustomId == null || !e.Interaction.Data.CustomId.StartsWith("teamsetup_")) return;

        var member = e.Interaction.User as DiscordMember;
        if (e.Interaction.GuildId == null || member == null || !member.PermissionsIn(e.Channel).HasPermission(DiscordPermission.ManageGuild))
        {
            DiscordInteractionResponseBuilder resp = new DiscordInteractionResponseBuilder();
            resp.WithContent("Manage Guild permission required.");
            resp.AsEphemeral();
            await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource, resp);
            return;
        }

        var guildId = e.Interaction.GuildId.Value;

        if (e.Interaction.Data.CustomId == "teamsetup_custom_text")
        {
            await ShowCustomTextPanelAsync(e);
            return;
        }

        if (e.Interaction.Data.CustomId.StartsWith("teamsetup_edit_"))
        {
            await HandleEditActionAsync(client, e);
            return;
        }

        if (e.Interaction.Data.CustomId == "teamsetup_back")
        {
            await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.DeferredMessageUpdate);
            using (var scope = _serviceProvider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var config = await db.TeamListConfigs.FirstOrDefaultAsync(c => c.GuildId == guildId);
                await RefreshSetupPanelAsync(e, config);
            }
            return;
        }

        await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.DeferredMessageUpdate);

        using (var scope = _serviceProvider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var config = await db.TeamListConfigs.FirstOrDefaultAsync(c => c.GuildId == guildId);
            if (config == null)
            {
                config = new TeamListConfig { GuildId = guildId };
                db.TeamListConfigs.Add(config);
            }

            if (e.Interaction.Data.CustomId == "teamsetup_channel")
            {
                if (ulong.TryParse(e.Interaction.Data.Values[0], out var channelId))
                {
                    config.ChannelId = channelId;
                    config.MessageId = null;
                }
            }
            else if (e.Interaction.Data.CustomId == "teamsetup_roles")
            {
                var roleIds = e.Interaction.Data.Values.Select(ulong.Parse).ToArray();
                config.TrackedRoles = roleIds;
            }

            await db.SaveChangesAsync();

            if (e.Interaction.Data.CustomId == "teamsetup_refresh")
            {
                await _teamListService.RefreshTeamListAsync(client, guildId);
            }

            await RefreshSetupPanelAsync(e, config);
        }
    }

    private async Task ShowCustomTextPanelAsync(ComponentInteractionCreatedEventArgs e)
    {
        var guildId = e.Interaction.GuildId!.Value;
        using (var scope = _serviceProvider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var config = await db.TeamListConfigs.FirstOrDefaultAsync(c => c.GuildId == guildId);

            DiscordEmbedBuilder embed = new DiscordEmbedBuilder();
            embed.WithFeatureTitle("Teams", "List Customization", "📋");
            embed.WithColor(SectorUI.SectorPrimary);
            embed.WithDescription("Edit the team list header, body, and footer.");

            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"**Header (Title):** {config?.CustomTitle ?? "Default (📋 Team List)"}");
            sb.AppendLine($"**Body Template:** {(string.IsNullOrWhiteSpace(config?.CustomTemplate) ? "Using default template" : "Custom template enabled")}");
            sb.AppendLine($"**Footer:** {config?.CustomFooter ?? "❌ Not configured"}");
            embed.AddField("Text Configuration", sb.ToString());
            embed.AddField("Template Tokens",
                "Preferred: `{role_name}`, `{role_mention}`, `{member_count}`, `{members_list}`\n" +
                "Legacy: `{RoleName}`, `{RoleMention}`, `{MemberCount}`, `{MembersList}`, `{rank}`, `{count}`, `{text}`",
                false);

            DiscordButtonComponent btnTitle = new DiscordButtonComponent(DiscordButtonStyle.Secondary, "teamsetup_edit_title", "Edit Title");
            DiscordButtonComponent btnBody = new DiscordButtonComponent(DiscordButtonStyle.Secondary, "teamsetup_edit_body", "Edit Body (Template)");
            DiscordButtonComponent btnFooter = new DiscordButtonComponent(DiscordButtonStyle.Secondary, "teamsetup_edit_footer", "Edit Footer");
            DiscordButtonComponent btnBack = new DiscordButtonComponent(DiscordButtonStyle.Danger, "teamsetup_back", "Back");

            if (e.Interaction.ResponseState == DiscordInteractionResponseState.Unacknowledged)
            {
                DiscordInteractionResponseBuilder resp = new DiscordInteractionResponseBuilder();
                resp.AddEmbed(embed.Build());
                resp.AddActionRowComponent(new DiscordActionRowComponent(new DiscordComponent[] { btnTitle, btnBody, btnFooter }));
                resp.AddActionRowComponent(new DiscordActionRowComponent(new DiscordComponent[] { btnBack }));
                await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.UpdateMessage, resp);
            }
            else
            {
                DiscordWebhookBuilder webhook = new DiscordWebhookBuilder();
                webhook.AddEmbed(embed.Build());
                webhook.AddActionRowComponent(new DiscordActionRowComponent(new DiscordComponent[] { btnTitle, btnBody, btnFooter }));
                webhook.AddActionRowComponent(new DiscordActionRowComponent(new DiscordComponent[] { btnBack }));
                await e.Interaction.EditOriginalResponseAsync(webhook);
            }
        }
    }

    private async Task HandleEditActionAsync(DiscordClient client, ComponentInteractionCreatedEventArgs e)
    {
        var field = e.Interaction.Data.CustomId.Replace("teamsetup_edit_", "");
        var guildId = e.Interaction.GuildId!.Value;

        DiscordInteractionResponseBuilder prompt = new DiscordInteractionResponseBuilder();
        var promptText = $"Enter the new **{field}** value. Type `cancel` to stop.";
        if (field == "body")
        {
            promptText += "\nPreferred tokens: `{role_name}`, `{role_mention}`, `{member_count}`, `{members_list}`. Legacy placeholders still work.";
        }

        prompt.WithContent(promptText);
        prompt.AsEphemeral();
        await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource, prompt);

        var interactivity = client.ServiceProvider.GetRequiredService<InteractivityExtension>();
        var msg = await interactivity.WaitForMessageAsync(x => x.Author.Id == e.User.Id && x.ChannelId == e.Channel.Id, TimeSpan.FromMinutes(2));

        if (msg.TimedOut || msg.Result == null || string.Equals(msg.Result.Content, "cancel", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var newValue = msg.Result.Content;
        try { await msg.Result.DeleteAsync(); } catch { }

        using (var scope = _serviceProvider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var config = await db.TeamListConfigs.FirstOrDefaultAsync(c => c.GuildId == guildId);
            if (config == null)
            {
                config = new TeamListConfig { GuildId = guildId };
                db.TeamListConfigs.Add(config);
            }

            if (field == "title") config.CustomTitle = newValue;
            else if (field == "body") config.CustomTemplate = newValue;
            else if (field == "footer") config.CustomFooter = newValue;

            await db.SaveChangesAsync();
            await _teamListService.RefreshTeamListAsync(client, guildId);
            await UpdateCustomTextPanelAsync(e, config);
        }
    }

    private async Task UpdateCustomTextPanelAsync(ComponentInteractionCreatedEventArgs e, TeamListConfig config)
    {
        DiscordEmbedBuilder embed = new DiscordEmbedBuilder();
        embed.WithFeatureTitle("Teams", "List Customization", "📋");
        embed.WithColor(SectorUI.SectorPrimary);
        embed.WithDescription("Edit the team list header, body, and footer.");

        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"**Header (Title):** {config?.CustomTitle ?? "Default (📋 Team List)"}");
        sb.AppendLine($"**Body Template:** {(string.IsNullOrWhiteSpace(config?.CustomTemplate) ? "Using default template" : "Custom template enabled")}");
        sb.AppendLine($"**Footer:** {config?.CustomFooter ?? "❌ Not configured"}");
        embed.AddField("Text Configuration", sb.ToString());
        embed.AddField("Template Tokens",
            "Preferred: `{role_name}`, `{role_mention}`, `{member_count}`, `{members_list}`\n" +
            "Legacy: `{RoleName}`, `{RoleMention}`, `{MemberCount}`, `{MembersList}`, `{rank}`, `{count}`, `{text}`",
            false);

        DiscordButtonComponent btnTitle = new DiscordButtonComponent(DiscordButtonStyle.Secondary, "teamsetup_edit_title", "Edit Title");
        DiscordButtonComponent btnBody = new DiscordButtonComponent(DiscordButtonStyle.Secondary, "teamsetup_edit_body", "Edit Body (Template)");
        DiscordButtonComponent btnFooter = new DiscordButtonComponent(DiscordButtonStyle.Secondary, "teamsetup_edit_footer", "Edit Footer");
        DiscordButtonComponent btnBack = new DiscordButtonComponent(DiscordButtonStyle.Danger, "teamsetup_back", "Back");

        DiscordMessageBuilder msg = new DiscordMessageBuilder();
        msg.AddEmbed(embed.Build());
        msg.AddActionRowComponent(new DiscordActionRowComponent(new DiscordComponent[] { btnTitle, btnBody, btnFooter }));
        msg.AddActionRowComponent(new DiscordActionRowComponent(new DiscordComponent[] { btnBack }));
        await e.Message.ModifyAsync(msg);
    }

    private async Task RefreshSetupPanelAsync(ComponentInteractionCreatedEventArgs e, TeamListConfig? config)
    {
        DiscordEmbedBuilder embed = new DiscordEmbedBuilder();
        embed.WithFeatureTitle("Teams", "Dynamic List Setup", "📋");
        embed.WithColor(SectorUI.SectorPrimary);
        embed.WithDescription("Configure the dynamic team list message.");

        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"**Target Channel:** {(config?.ChannelId > 0 ? $"<#{config.ChannelId}>" : "❌ Not configured")}");
        
        var rolesStr = config?.TrackedRoles?.Length > 0 
            ? string.Join(", ", config.TrackedRoles.Select(r => $"<@&{r}>")) 
            : "❌ Not configured";
        sb.AppendLine($"**Tracked Roles:** {rolesStr}");
        sb.AppendLine($"**Title:** {config?.CustomTitle ?? "Default"}");
        sb.AppendLine($"**Template:** {(string.IsNullOrWhiteSpace(config?.CustomTemplate) ? "Using default template" : "Custom template enabled")}");
        sb.AppendLine($"**Footer:** {(string.IsNullOrWhiteSpace(config?.CustomFooter) ? "❌ Not configured" : "✅ Configured")}");
        sb.AppendLine("**Template Tokens:** `{role_name}`, `{role_mention}`, `{member_count}`, `{members_list}`");
        embed.AddField("Current Setup", sb.ToString());

        DiscordChannelSelectComponent channelSelect = new DiscordChannelSelectComponent("teamsetup_channel", "Select target channel...");
        DiscordRoleSelectComponent roleSelect = new DiscordRoleSelectComponent("teamsetup_roles", "Select roles to track...", false, 1, 10);
        DiscordButtonComponent btnCustomText = new DiscordButtonComponent(DiscordButtonStyle.Primary, "teamsetup_custom_text", "Customize Text");
        DiscordButtonComponent btnRefresh = new DiscordButtonComponent(DiscordButtonStyle.Success, "teamsetup_refresh", "Force Refresh");

        DiscordWebhookBuilder webhook = new DiscordWebhookBuilder();
        webhook.AddEmbed(embed.Build());
        webhook.AddActionRowComponent(new DiscordActionRowComponent(new DiscordComponent[] { channelSelect }));
        webhook.AddActionRowComponent(new DiscordActionRowComponent(new DiscordComponent[] { roleSelect }));
        webhook.AddActionRowComponent(new DiscordActionRowComponent(new DiscordComponent[] { btnCustomText, btnRefresh }));

        await e.Interaction.EditOriginalResponseAsync(webhook);
    }
}
