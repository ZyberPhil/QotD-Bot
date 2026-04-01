using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using DSharpPlus.Interactivity;
using DSharpPlus.Interactivity.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QotD.Bot.Data;
using QotD.Bot.Data.Models;
using System.Text;

namespace QotD.Bot.Features.Teams.Services;

public sealed class TeamSetupEventHandler : 
    IEventHandler<ComponentInteractionCreatedEventArgs>,
    IEventHandler<ModalSubmittedEventArgs>
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
            await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource, 
                new DiscordInteractionResponseBuilder().WithContent("Manage Guild permission required.").AsEphemeral());
            return;
        }

        var guildId = e.Interaction.GuildId.Value;

        if (e.Interaction.Data.CustomId == "teamsetup_template")
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var config = await db.TeamListConfigs.FirstOrDefaultAsync(c => c.GuildId == guildId);
            
            var modal = new DiscordInteractionResponseBuilder()
                .WithTitle("Edit Team List Template")
                .WithCustomId("teamsetup_modal")
                .AddActionRowComponent(new TextInputComponent("Custom Title", "title", "📋 Team List", config?.CustomTitle, false))
                .AddActionRowComponent(new TextInputComponent("Body Template", "template", "{rank} ({count})\n{text}", config?.CustomTemplate, true, TextInputStyle.Paragraph))
                .AddActionRowComponent(new TextInputComponent("Custom Footer", "footer", "Optional footer text", config?.CustomFooter, false));

            await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.Modal, modal);
            return;
        }

        await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.DeferredMessageUpdate);

        using var scope = _serviceProvider.CreateScope();
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
                config.MessageId = null; // reset message ID as it moved channel
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

    public async Task HandleEventAsync(DiscordClient client, ModalSubmittedEventArgs e)
    {
        if (e.Interaction.Data.CustomId != "teamsetup_modal") return;

        var guildId = e.Interaction.GuildId!.Value;
        
        await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.DeferredMessageUpdate);

        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var config = await db.TeamListConfigs.FirstOrDefaultAsync(c => c.GuildId == guildId);
        if (config == null)
        {
            config = new TeamListConfig { GuildId = guildId };
            db.TeamListConfigs.Add(config);
        }

        config.CustomTitle = e.Values["title"];
        config.CustomTemplate = e.Values["template"];
        config.CustomFooter = e.Values["footer"];

        await db.SaveChangesAsync();

        await _teamListService.RefreshTeamListAsync(client, guildId);
        
        // We need to edit the ORIGINAL message that opened the modal. 
        // In modal submit, e.Interaction is the modal interaction.
        // We might not be able to easily get the original message here if it was a component interaction session.
        // However, we can use EditOriginalResponseAsync if we have the right context.
        // Usually, the setup panel is edited.
        await e.Interaction.EditOriginalResponseAsync(new DiscordWebhookBuilder().WithContent("✅ Configuration updated!"));
    }

    private async Task RefreshSetupPanelAsync(ComponentInteractionCreatedEventArgs e, TeamListConfig config)
    {
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
        var roleSelect = new DiscordRoleSelectComponent("teamsetup_roles", "Select roles to track...", false, 1, 10);
        if (config?.TrackedRoles != null && config.TrackedRoles.Length > 0)
        {
            var defaults = config.TrackedRoles
                .Select(id => new DiscordSelectDefaultValue(id, DiscordSelectDefaultValueType.Role));
            
            if (roleSelect.DefaultValues is List<DiscordSelectDefaultValue> list)
            {
                list.AddRange(defaults);
            }
        }

        var btnTemplate = new DiscordButtonComponent(DiscordButtonStyle.Primary, "teamsetup_template", "Edit Template");
        var btnRefresh = new DiscordButtonComponent(DiscordButtonStyle.Success, "teamsetup_refresh", "Force Refresh List");

        await e.Interaction.EditOriginalResponseAsync(new DiscordWebhookBuilder()
            .AddEmbed(embed)
            .AddActionRowComponent(new DiscordActionRowComponent(new DiscordComponent[] { channelSelect }))
            .AddActionRowComponent(new DiscordActionRowComponent(new DiscordComponent[] { roleSelect }))
            .AddActionRowComponent(new DiscordActionRowComponent(new DiscordComponent[] { btnTemplate, btnRefresh })));
    }
}
