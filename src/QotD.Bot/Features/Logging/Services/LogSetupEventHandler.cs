using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QotD.Bot.Data;
using QotD.Bot.Data.Models;
using QotD.Bot.Features.Logging.Models;
using QotD.Bot.UI;
using System.Text;

namespace QotD.Bot.Features.Logging.Services;

public sealed class LogSetupEventHandler : IEventHandler<ComponentInteractionCreatedEventArgs>
{
    private readonly IServiceScopeFactory _scopeFactory;
    private static readonly LogType[] ConfigurableLogTypes =
    [
        LogType.MessageDeleted,
        LogType.MessageUpdated,
        LogType.MemberJoinLeave,
        LogType.VoiceJoinLeave,
        LogType.BotAction,
        LogType.BotError
    ];

    public LogSetupEventHandler(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task HandleEventAsync(DiscordClient client, ComponentInteractionCreatedEventArgs e)
    {
        if (e.Interaction.Data.CustomId is null || !e.Interaction.Data.CustomId.StartsWith("logsetup_"))
            return;

        // Ensure user is admin
        var member = e.Interaction.User as DiscordMember;
        if (e.Interaction.GuildId == null || member == null || !member.PermissionsIn(e.Channel).HasPermission(DiscordPermission.ManageGuild))
        {
            await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource, 
                new DiscordInteractionResponseBuilder().WithContent("You must have Manage Guild permission to do this.").AsEphemeral());
            return;
        }

        await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.DeferredMessageUpdate);

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var guildId = e.Interaction.GuildId.Value;

        if (e.Interaction.Data.CustomId == "logsetup_close")
        {
            await e.Interaction.EditOriginalResponseAsync(new DiscordWebhookBuilder().WithContent("Logging setup closed."));
            return;
        }

        if (e.Interaction.Data.CustomId == "logsetup_back")
        {
            await RenderMainMenuAsync(e, db, guildId);
            return;
        }

        if (e.Interaction.Data.CustomId == "logsetup_typeselect")
        {
            var selectedTypeStr = e.Interaction.Data.Values[0];
            if (Enum.TryParse<LogType>(selectedTypeStr, out var selectedType) && ConfigurableLogTypes.Contains(selectedType))
            {
                var channelSelect = new DiscordChannelSelectComponent($"logsetup_channel_{selectedType}", "Select destination channel");
                var btnDisable = new DiscordButtonComponent(DiscordButtonStyle.Secondary, $"logsetup_disable_{selectedType}", "Disable / Unmap");
                var btnBack = new DiscordButtonComponent(DiscordButtonStyle.Primary, "logsetup_back", "Back");

                var channelEmbed = SectorUI.CreateBaseEmbed($"⚙️ Route {selectedType}")
                    .WithDescription($"Select the channel where **{selectedType}** logs should be sent.\nYou can map multiple types to the same channel.")
                    .WithColor(SectorUI.SectorPrimary);

                await e.Interaction.EditOriginalResponseAsync(new DiscordWebhookBuilder()
                    .AddEmbed(channelEmbed)
                    .AddActionRowComponent(new DiscordActionRowComponent(new DiscordComponent[] { channelSelect }))
                    .AddActionRowComponent(new DiscordActionRowComponent(new DiscordComponent[] { btnDisable, btnBack })));
            }
            return;
        }

        if (e.Interaction.Data.CustomId.StartsWith("logsetup_disable_"))
        {
            var typeStr = e.Interaction.Data.CustomId.Replace("logsetup_disable_", "");
            if (Enum.TryParse<LogType>(typeStr, out var selectedType) && ConfigurableLogTypes.Contains(selectedType))
            {
                var config = await db.LogRoutingConfigs.FirstOrDefaultAsync(c => c.GuildId == guildId && c.LogType == selectedType);
                if (config != null)
                {
                    db.LogRoutingConfigs.Remove(config);
                    await db.SaveChangesAsync();
                }
                await RenderMainMenuAsync(e, db, guildId);
            }
            return;
        }

        if (e.Interaction.Data.CustomId.StartsWith("logsetup_channel_"))
        {
            var typeStr = e.Interaction.Data.CustomId.Replace("logsetup_channel_", "");
            if (Enum.TryParse<LogType>(typeStr, out var selectedType) && ConfigurableLogTypes.Contains(selectedType))
            {
                var selectedChannelIdStr = e.Interaction.Data.Values[0];
                if (ulong.TryParse(selectedChannelIdStr, out var channelId))
                {
                    var config = await db.LogRoutingConfigs.FirstOrDefaultAsync(c => c.GuildId == guildId && c.LogType == selectedType);
                    if (config == null)
                    {
                        config = new LogRoutingConfig
                        {
                            GuildId = guildId,
                            LogType = selectedType,
                            ChannelId = channelId,
                            IsEnabled = true
                        };
                        db.LogRoutingConfigs.Add(config);
                    }
                    else
                    {
                        config.ChannelId = channelId;
                        config.IsEnabled = true;
                    }
                    await db.SaveChangesAsync();
                    await RenderMainMenuAsync(e, db, guildId);
                }
            }
        }
    }

    private async Task RenderMainMenuAsync(ComponentInteractionCreatedEventArgs e, AppDbContext db, ulong guildId)
    {
        var configs = await db.LogRoutingConfigs
            .Where(c => c.GuildId == guildId)
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

        await e.Interaction.EditOriginalResponseAsync(new DiscordWebhookBuilder()
            .AddEmbed(embed)
            .AddActionRowComponent(new DiscordActionRowComponent(new DiscordComponent[] { typeSelect }))
            .AddActionRowComponent(new DiscordActionRowComponent(new DiscordComponent[] { btnClose })));
    }
}
