using System.ComponentModel;
using DSharpPlus;
using DSharpPlus.Commands;
using DSharpPlus.Commands.ContextChecks;
using DSharpPlus.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QotD.Bot.Data;
using QotD.Bot.Data.Models;
using QotD.Bot.Features.SelfRoles.Services;
using QotD.Bot.UI;

namespace QotD.Bot.Features.SelfRoles.Commands;

[Command("selfrolesetup")]
[Description("Configure self-assignable roles and the reaction panel")]
public sealed class SelfRolesSetupCommand
{
    private readonly AppDbContext _db;
    private readonly SelfRoleService _service;

    public SelfRolesSetupCommand(AppDbContext db, SelfRoleService service)
    {
        _db = db;
        _service = service;
    }

    [Command("status")]
    [RequirePermissions(DiscordPermission.ManageGuild)]
    [Description("Show the current self-role configuration")]
    public async ValueTask StatusAsync(CommandContext ctx)
    {
        if (ctx.Guild is null)
        {
            await ctx.RespondAsync("Dieser Befehl kann nur in einem Server genutzt werden.");
            return;
        }

        var config = await _db.SelfRoleConfigs.FirstOrDefaultAsync(x => x.GuildId == ctx.Guild.Id);
        var options = await _db.SelfRoleOptions.Where(x => x.GuildId == ctx.Guild.Id).ToListAsync();
        var groups = await _db.SelfRoleGroups.Where(x => x.GuildId == ctx.Guild.Id).ToListAsync();

        var embed = new DiscordEmbedBuilder()
            .WithFeatureTitle("Self Roles", "Configuration Status", "🎭")
            .WithColor(SectorUI.SectorPrimary)
            .WithDescription("Current setup for the self-role panel.")
            .AddField("Panel", $"Channel: {(config?.PanelChannelId is > 0 ? $"<#{config.PanelChannelId}>" : "not set")}\nMessage: {(config?.PanelMessageId is > 0 ? config.PanelMessageId!.Value.ToString() : "not published")}", true)
            .AddField("Rules", $"Enabled: {(config?.IsEnabled == true ? "yes" : "no")}\nMultiple roles: {(config?.AllowMultipleRoles == true ? "yes" : "no")}\nModeration: {(config?.RequireModeration == true ? "yes" : "no")}", true)
            .AddField("Content", $"Title: {(string.IsNullOrWhiteSpace(config?.PanelTitle) ? "default" : config!.PanelTitle)}\nOptions: {options.Count}\nGroups: {groups.Count}", true)
            .WithTimestamp(DateTimeOffset.UtcNow);

        await ctx.RespondAsync(new DiscordMessageBuilder().AddEmbed(embed));
    }

    [Command("panel")]
    [RequirePermissions(DiscordPermission.ManageGuild)]
    [Description("Configure the self-role panel appearance and behavior")]
    public async ValueTask PanelAsync(
        CommandContext ctx,
        [Description("Channel where the panel should live")] DiscordChannel channel,
        [Description("Whether the panel is enabled")] bool enabled = true,
        [Description("Allow multiple roles per user")] bool allowMultipleRoles = true,
        [Description("Require moderation approval for flagged roles")] bool requireModeration = false,
        [Description("Moderation channel for pending requests")] DiscordChannel? moderationChannel = null,
        [Description("Panel title")]
        string? title = null,
        [Description("Panel description template")]
        string? descriptionTemplate = null,
        [Description("Panel footer")]
        string? footer = null,
        [Description("Panel color in hex, for example #5865F2")]
        string? colorHex = null,
        [Description("Panel thumbnail URL")]
        string? thumbnailUrl = null,
        [Description("Panel image URL")]
        string? imageUrl = null)
    {
        if (ctx.Guild is null)
        {
            await ctx.RespondAsync("Dieser Befehl kann nur in einem Server genutzt werden.");
            return;
        }

        var config = await _service.GetOrCreateConfigAsync(ctx.Guild.Id);
        config.PanelChannelId = channel.Id;
        config.IsEnabled = enabled;
        config.AllowMultipleRoles = allowMultipleRoles;
        config.RequireModeration = requireModeration;
        config.ModerationChannelId = moderationChannel?.Id;
        config.PanelTitle = string.IsNullOrWhiteSpace(title) ? config.PanelTitle : title;
        config.PanelDescriptionTemplate = string.IsNullOrWhiteSpace(descriptionTemplate) ? config.PanelDescriptionTemplate : descriptionTemplate;
        config.PanelFooter = string.IsNullOrWhiteSpace(footer) ? config.PanelFooter : footer;
        config.PanelColorHex = string.IsNullOrWhiteSpace(colorHex) ? config.PanelColorHex : colorHex;
        config.PanelThumbnailUrl = string.IsNullOrWhiteSpace(thumbnailUrl) ? config.PanelThumbnailUrl : thumbnailUrl;
        config.PanelImageUrl = string.IsNullOrWhiteSpace(imageUrl) ? config.PanelImageUrl : imageUrl;

        await _db.SaveChangesAsync();
        await ctx.RespondAsync($"✅ Self-role panel updated for {channel.Mention}.");
    }

    [Command("group")]
    [RequirePermissions(DiscordPermission.ManageGuild)]
    [Description("Create or update a self-role group")]
    public async ValueTask GroupAsync(
        CommandContext ctx,
        [Description("Group name")] string name,
        [Description("Whether the group is exclusive")] bool exclusive = true,
        [Description("Display order")] int order = 0)
    {
        if (ctx.Guild is null)
        {
            await ctx.RespondAsync("Dieser Befehl kann nur in einem Server genutzt werden.");
            return;
        }

        var guildId = ctx.Guild.Id;
        var group = await _db.SelfRoleGroups.FirstOrDefaultAsync(x => x.GuildId == guildId && x.Name == name);
        if (group is null)
        {
            group = new SelfRoleGroup { GuildId = guildId, Name = name };
            _db.SelfRoleGroups.Add(group);
        }

        group.IsExclusive = exclusive;
        group.DisplayOrder = order;
        await _db.SaveChangesAsync();
        await ctx.RespondAsync($"✅ Group '{name}' saved.");
    }

    [Command("optionadd")]
    [RequirePermissions(DiscordPermission.ManageGuild)]
    [Description("Add or update a self-role option")]
    public async ValueTask OptionAddAsync(
        CommandContext ctx,
        [Description("Role to assign")] DiscordRole role,
        [Description("Emoji shown for the role, unicode or custom emoji")]
        string emoji,
        [Description("Display label")]
        string label,
        [Description("Short description")]
        string? description = null,
        [Description("Display order")]
        int order = 0,
        [Description("Optional group name")]
        string? groupName = null,
        [Description("Whether this role requires moderation approval")]
        bool requiresApproval = false)
    {
        if (ctx.Guild is null)
        {
            await ctx.RespondAsync("Dieser Befehl kann nur in einem Server genutzt werden.");
            return;
        }

        var option = await _service.UpsertOptionAsync(ctx.Client, ctx.Guild, role, emoji, label, description, order, groupName, requiresApproval);
        await ctx.RespondAsync($"✅ Self-role option saved for {role.Mention} ({option.EmojiKey}).");
    }

    [Command("optionremove")]
    [RequirePermissions(DiscordPermission.ManageGuild)]
    [Description("Remove a self-role option by role")]
    public async ValueTask OptionRemoveAsync(CommandContext ctx, [Description("Role to remove from the panel")] DiscordRole role)
    {
        if (ctx.Guild is null)
        {
            await ctx.RespondAsync("Dieser Befehl kann nur in einem Server genutzt werden.");
            return;
        }

        var option = await _db.SelfRoleOptions.FirstOrDefaultAsync(x => x.GuildId == ctx.Guild.Id && x.RoleId == role.Id);
        if (option is null)
        {
            await ctx.RespondAsync("Role option not found.");
            return;
        }

        _db.SelfRoleOptions.Remove(option);
        await _db.SaveChangesAsync();
        await ctx.RespondAsync($"✅ Removed self-role option for {role.Mention}.");
    }

    [Command("publish")]
    [RequirePermissions(DiscordPermission.ManageGuild)]
    [Description("Render or refresh the self-role panel")]
    public async ValueTask PublishAsync(CommandContext ctx)
    {
        if (ctx.Guild is null)
        {
            await ctx.RespondAsync("Dieser Befehl kann nur in einem Server genutzt werden.");
            return;
        }

        await _service.RefreshPanelAsync(ctx.Client, ctx.Guild.Id);
        await ctx.RespondAsync("✅ Self-role panel refreshed.");
    }
}