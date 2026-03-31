using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
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
            // Open a Modal
            var modal = new DiscordModalBuilder();
            modal.CustomId = "teamsetup_template_modal";
            modal.Title = "Edit Custom Template";
            
            modal.AddTextInput(new DiscordTextInputComponent(
                "Template String", 
                "template_text", 
                "Enter template...", 
                false,
                DiscordTextInputStyle.Paragraph,
                0,
                2000), "Template String", "Customize the team list format");

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
        if (e.Interaction.Data.CustomId != "teamsetup_template_modal") return;

        var member = e.Interaction.User as DiscordMember;
        if (e.Interaction.GuildId == null || member == null || e.Interaction.Channel == null || !member.PermissionsIn(e.Interaction.Channel).HasPermission(DiscordPermission.ManageGuild)) return;

        await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.DeferredMessageUpdate);

        string? templateText = null;
        if (e.Values.TryGetValue("template_text", out var submission))
        {
            if (submission is DSharpPlus.EventArgs.TextInputModalSubmission textInput)
            {
                templateText = textInput.Value;
            }
        }

        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var config = await db.TeamListConfigs.FirstOrDefaultAsync(c => c.GuildId == e.Interaction.GuildId.Value);
        if (config != null)
        {
            config.CustomTemplate = string.IsNullOrWhiteSpace(templateText) ? null : templateText;
            await db.SaveChangesAsync();
            
            // We can't trivially use e.Interaction.EditOriginalResponseAsync to update the panel unless we fetch it.
            // But we can trigger a refresh of the actual team list message
            await _teamListService.RefreshTeamListAsync(client, e.Interaction.GuildId.Value);
        }
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
        
        var templateDisplay = string.IsNullOrWhiteSpace(config?.CustomTemplate) 
            ? "Default Template" 
            : "Custom Template Active";
        sb.AppendLine($"**Template:** {templateDisplay}");
        
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
