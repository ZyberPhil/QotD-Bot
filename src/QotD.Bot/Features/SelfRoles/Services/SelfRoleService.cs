using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using QotD.Bot.Data;
using QotD.Bot.Data.Models;
using QotD.Bot.UI;
using System.Text;

namespace QotD.Bot.Features.SelfRoles.Services;

public sealed class SelfRoleService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SelfRoleService> _logger;

    public SelfRoleService(IServiceProvider serviceProvider, ILogger<SelfRoleService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task<SelfRoleConfig> GetOrCreateConfigAsync(ulong guildId)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var config = await db.SelfRoleConfigs.FirstOrDefaultAsync(x => x.GuildId == guildId);
        if (config is null)
        {
            config = new SelfRoleConfig { GuildId = guildId };
            db.SelfRoleConfigs.Add(config);
        }

        return config;
    }

    public async Task<SelfRoleOption> UpsertOptionAsync(
        DiscordClient client,
        DiscordGuild guild,
        DiscordRole role,
        string emoji,
        string label,
        string? description,
        int displayOrder,
        string? groupName,
        bool requiresApproval)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var config = await db.SelfRoleConfigs.FirstOrDefaultAsync(x => x.GuildId == guild.Id);
        if (config is null)
        {
            config = new SelfRoleConfig { GuildId = guild.Id };
            db.SelfRoleConfigs.Add(config);
        }

        var group = await ResolveGroupAsync(db, guild.Id, groupName);
        var emojiKey = NormalizeEmojiKey(client, emoji);

        var option = await db.SelfRoleOptions.FirstOrDefaultAsync(x => x.GuildId == guild.Id && x.RoleId == role.Id);
        if (option is null)
        {
            option = new SelfRoleOption { GuildId = guild.Id, RoleId = role.Id };
            db.SelfRoleOptions.Add(option);
        }

        option.EmojiKey = emojiKey;
        option.Label = label;
        option.Description = description;
        option.DisplayOrder = displayOrder;
        option.RequiresApproval = requiresApproval;
        option.GroupId = group?.Id;

        await db.SaveChangesAsync();
        return option;
    }

    public async Task RefreshPanelAsync(DiscordClient client, ulong guildId)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var config = await db.SelfRoleConfigs.FirstOrDefaultAsync(x => x.GuildId == guildId);
        if (config is null || !config.IsEnabled || config.PanelChannelId is null)
        {
            return;
        }

        if (!client.Guilds.TryGetValue(guildId, out var guild))
        {
            return;
        }

        if (!guild.Channels.TryGetValue(config.PanelChannelId.Value, out var channel))
        {
            return;
        }

        var options = await db.SelfRoleOptions
            .AsNoTracking()
            .Where(x => x.GuildId == guildId)
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.Id)
            .ToListAsync();

        if (options.Count == 0)
        {
            return;
        }

        var groups = await db.SelfRoleGroups
            .AsNoTracking()
            .Where(x => x.GuildId == guildId)
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.Name)
            .ToListAsync();

        var embed = BuildPanelEmbed(config, groups, options, guild);

        DiscordMessage? message = null;
        if (config.PanelMessageId is > 0)
        {
            try
            {
                message = await channel.GetMessageAsync(config.PanelMessageId.Value);
            }
            catch
            {
                message = null;
            }
        }

        if (message is null)
        {
            message = await channel.SendMessageAsync(embed.Build());
            config.PanelMessageId = message.Id;
            await db.SaveChangesAsync();
        }
        else
        {
            await message.ModifyAsync(new DiscordMessageBuilder().AddEmbed(embed.Build()));
            try
            {
                await message.DeleteAllReactionsAsync("Refreshing self-role panel reactions");
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Unable to clear self-role reactions for guild {GuildId}", guildId);
            }
        }

        await SyncPanelReactionsAsync(client, message, options);
    }

    public async Task HandleReactionAddedAsync(DiscordClient client, MessageReactionAddedEventArgs e)
    {
        if (e.Guild is null || e.Message is null || e.User is null || e.User.IsBot)
        {
            return;
        }

        var config = await GetConfigAsync(e.Guild.Id);
        if (config is null || !config.IsEnabled || config.PanelMessageId is null || e.Message.Id != config.PanelMessageId.Value)
        {
            return;
        }

        var option = await FindOptionAsync(e.Guild.Id, e.Emoji.GetDiscordName());
        if (option is null)
        {
            return;
        }

        var member = await ResolveMemberAsync(e.Guild, e.User.Id);
        if (member is null)
        {
            return;
        }

        await ApplyOptionAsync(client, e.Guild, member, option, addedByReaction: true);
    }

    public async Task HandleReactionRemovedAsync(DiscordClient client, MessageReactionRemovedEventArgs e)
    {
        if (e.Guild is null || e.Message is null || e.User is null || e.User.IsBot)
        {
            return;
        }

        var config = await GetConfigAsync(e.Guild.Id);
        if (config is null || !config.IsEnabled || config.PanelMessageId is null || e.Message.Id != config.PanelMessageId.Value)
        {
            return;
        }

        var option = await FindOptionAsync(e.Guild.Id, e.Emoji.GetDiscordName());
        if (option is null)
        {
            return;
        }

        var member = await ResolveMemberAsync(e.Guild, e.User.Id);
        if (member is null)
        {
            return;
        }

        await RemoveOptionAsync(client, e.Guild, member, option, removedByReaction: true);
    }

    public async Task HandleModerationButtonAsync(DiscordClient client, ComponentInteractionCreatedEventArgs e)
    {
        if (e.Interaction.GuildId is null || e.Interaction.User is not DiscordMember member || e.Interaction.Data.CustomId is null)
        {
            return;
        }

        if (!e.Interaction.Data.CustomId.StartsWith("selfroles_request_"))
        {
            return;
        }

        if (!member.PermissionsIn(e.Channel).HasPermission(DiscordPermission.ManageRoles) && !member.PermissionsIn(e.Channel).HasPermission(DiscordPermission.ManageGuild))
        {
            await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder().WithContent("Manage Roles or Manage Guild permission required.").AsEphemeral());
            return;
        }

        var parts = e.Interaction.Data.CustomId.Split('_', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 4 || !long.TryParse(parts[3], out var requestId))
        {
            return;
        }

        var approve = e.Interaction.Data.CustomId.Contains("approve", StringComparison.OrdinalIgnoreCase);
        var deny = e.Interaction.Data.CustomId.Contains("deny", StringComparison.OrdinalIgnoreCase);
        if (!approve && !deny)
        {
            return;
        }

        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var request = await db.SelfRoleRequests.FirstOrDefaultAsync(x => x.Id == requestId && x.GuildId == e.Interaction.GuildId.Value);
        if (request is null || request.Status != SelfRoleRequestStatus.Pending)
        {
            await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder().WithContent("This request is no longer pending.").AsEphemeral());
            return;
        }

        var guild = e.Interaction.Guild;
        if (guild is null)
        {
            return;
        }

        var targetMember = await ResolveMemberAsync(guild, request.UserId);
        if (targetMember is null)
        {
            request.Status = SelfRoleRequestStatus.Denied;
            request.Reason = "Member not found";
            request.ResolvedAtUtc = DateTimeOffset.UtcNow;
            request.ModeratorId = e.Interaction.User.Id;
            await db.SaveChangesAsync();

            await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder().WithContent("User is no longer available.").AsEphemeral());
            return;
        }

        var option = await db.SelfRoleOptions.FirstOrDefaultAsync(x => x.GuildId == guild.Id && x.RoleId == request.RoleId);
        if (option is null)
        {
            request.Status = SelfRoleRequestStatus.Denied;
            request.Reason = "Role option removed";
            request.ResolvedAtUtc = DateTimeOffset.UtcNow;
            request.ModeratorId = e.Interaction.User.Id;
            await db.SaveChangesAsync();
            await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder().WithContent("The role option was removed.").AsEphemeral());
            return;
        }

        if (approve)
        {
            await ApplyOptionAsync(client, guild, targetMember, option, addedByReaction: false);
            request.Status = SelfRoleRequestStatus.Approved;
            request.ResolvedAtUtc = DateTimeOffset.UtcNow;
            request.ModeratorId = e.Interaction.User.Id;
            request.Reason = "Approved by moderator";
        }
        else
        {
            request.Status = SelfRoleRequestStatus.Denied;
            request.ResolvedAtUtc = DateTimeOffset.UtcNow;
            request.ModeratorId = e.Interaction.User.Id;
            request.Reason = "Denied by moderator";
        }

        await db.SaveChangesAsync();

        var response = new DiscordInteractionResponseBuilder()
            .WithContent(approve ? $"✅ Approved <@{request.UserId}> for <@&{request.RoleId}>." : $"❌ Denied <@{request.UserId}> for <@&{request.RoleId}>.");
        response.ClearComponents();

        await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.UpdateMessage, response);
    }

    public async Task<SelfRoleConfig?> GetConfigAsync(ulong guildId)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.SelfRoleConfigs.FirstOrDefaultAsync(x => x.GuildId == guildId);
    }

    private async Task ApplyOptionAsync(DiscordClient client, DiscordGuild guild, DiscordMember member, SelfRoleOption option, bool addedByReaction)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var config = await db.SelfRoleConfigs.FirstOrDefaultAsync(x => x.GuildId == guild.Id);
        if (config is null || !config.IsEnabled)
        {
            return;
        }

        var role = guild.Roles.GetValueOrDefault(option.RoleId);
        if (role is null)
        {
            return;
        }

        var existingOptions = await db.SelfRoleOptions.AsNoTracking().Where(x => x.GuildId == guild.Id).ToListAsync();

        if (!config.AllowMultipleRoles)
        {
            foreach (var otherRoleId in existingOptions.Where(x => x.RoleId != option.RoleId).Select(x => x.RoleId))
            {
                var otherRole = guild.Roles.GetValueOrDefault(otherRoleId);
                if (otherRole is not null && member.Roles.Any(r => r.Id == otherRole.Id))
                {
                    try
                    {
                        await member.RevokeRoleAsync(otherRole, "Self-role single selection enforcement.");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Failed to revoke single-selection role {RoleId} from user {UserId}", otherRoleId, member.Id);
                    }
                }
            }
        }

        if (option.GroupId is not null)
        {
            var group = await db.SelfRoleGroups.AsNoTracking().FirstOrDefaultAsync(x => x.Id == option.GroupId.Value);
            if (group?.IsExclusive == true)
            {
                var groupedOptions = existingOptions.Where(x => x.GroupId == group.Id && x.RoleId != option.RoleId).ToList();
                foreach (var otherOption in groupedOptions)
                {
                    var otherRole = guild.Roles.GetValueOrDefault(otherOption.RoleId);
                    if (otherRole is not null && member.Roles.Any(r => r.Id == otherRole.Id))
                    {
                        try
                        {
                            await member.RevokeRoleAsync(otherRole, "Self-role exclusive group enforcement.");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogDebug(ex, "Failed to enforce exclusive group {GroupId} for user {UserId}", group.Id, member.Id);
                        }
                    }
                }
            }
        }

        if (member.Roles.Any(r => r.Id == role.Id))
        {
            return;
        }

        if (option.RequiresApproval || config.RequireModeration)
        {
            await CreateModerationRequestAsync(db, guild, member, option, config, addedByReaction ? "Reaction requested" : "Moderator request");
            return;
        }

        try
        {
            await member.GrantRoleAsync(role, "Self-role assignment.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to grant self-role {RoleId} to user {UserId}", role.Id, member.Id);
        }
    }

    private async Task RemoveOptionAsync(DiscordClient client, DiscordGuild guild, DiscordMember member, SelfRoleOption option, bool removedByReaction)
    {
        var role = guild.Roles.GetValueOrDefault(option.RoleId);
        if (role is null || !member.Roles.Any(r => r.Id == role.Id))
        {
            return;
        }

        try
        {
            await member.RevokeRoleAsync(role, removedByReaction ? "Self-role removed by reaction." : "Self-role removed by moderator action.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to revoke self-role {RoleId} from user {UserId}", role.Id, member.Id);
        }
    }

    private async Task CreateModerationRequestAsync(AppDbContext db, DiscordGuild guild, DiscordMember member, SelfRoleOption option, SelfRoleConfig config, string reason)
    {
        if (config.ModerationChannelId is null)
        {
            _logger.LogWarning("Moderation requested in guild {GuildId} but no moderation channel is configured.", guild.Id);
            return;
        }

        var hasPendingRequest = await db.SelfRoleRequests.AnyAsync(x =>
            x.GuildId == guild.Id &&
            x.UserId == member.Id &&
            x.RoleId == option.RoleId &&
            x.Status == SelfRoleRequestStatus.Pending);

        if (hasPendingRequest)
        {
            return;
        }

        var request = new SelfRoleRequest
        {
            GuildId = guild.Id,
            UserId = member.Id,
            RoleId = option.RoleId,
            PanelMessageId = config.PanelMessageId,
            ModerationChannelId = config.ModerationChannelId,
            Status = SelfRoleRequestStatus.Pending,
            Reason = reason
        };

        db.SelfRoleRequests.Add(request);
        await db.SaveChangesAsync();

        if (!guild.Channels.TryGetValue(config.ModerationChannelId.Value, out var moderationChannel))
        {
            return;
        }

        var embed = new DiscordEmbedBuilder()
            .WithFeatureTitle("Self Roles", "Pending Approval", "🎭")
            .WithColor(SectorUI.SectorWarning)
            .AddField("User", member.Mention, true)
            .AddField("Role", $"<@&{option.RoleId}>", true)
            .AddField("Reason", reason, false)
            .WithTimestamp(DateTimeOffset.UtcNow);

        var approve = new DiscordButtonComponent(DiscordButtonStyle.Success, $"selfroles_request_approve_{request.Id}", "Approve");
        var deny = new DiscordButtonComponent(DiscordButtonStyle.Danger, $"selfroles_request_deny_{request.Id}", "Deny");

        var message = await moderationChannel.SendMessageAsync(new DiscordMessageBuilder()
            .AddEmbed(embed)
            .AddActionRowComponent(new DiscordActionRowComponent(new DiscordComponent[] { approve, deny })));

        request.ModerationMessageId = message.Id;
        await db.SaveChangesAsync();
    }

    private async Task<DiscordMember?> ResolveMemberAsync(DiscordGuild guild, ulong userId)
    {
        try
        {
            return await guild.GetMemberAsync(userId);
        }
        catch
        {
            return null;
        }
    }

    private async Task<SelfRoleOption?> FindOptionAsync(ulong guildId, string emojiKey)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.SelfRoleOptions.FirstOrDefaultAsync(x => x.GuildId == guildId && x.EmojiKey == emojiKey);
    }

    private async Task<SelfRoleGroup?> ResolveGroupAsync(AppDbContext db, ulong guildId, string? groupName)
    {
        if (string.IsNullOrWhiteSpace(groupName))
        {
            return null;
        }

        var group = await db.SelfRoleGroups.FirstOrDefaultAsync(x => x.GuildId == guildId && x.Name == groupName);
        if (group is null)
        {
            group = new SelfRoleGroup { GuildId = guildId, Name = groupName };
            db.SelfRoleGroups.Add(group);
            await db.SaveChangesAsync();
        }

        return group;
    }

    private static DiscordEmbedBuilder BuildPanelEmbed(SelfRoleConfig config, IReadOnlyCollection<SelfRoleGroup> groups, IReadOnlyCollection<SelfRoleOption> options, DiscordGuild guild)
    {
        var embed = SectorUI.CreateBaseEmbed();
        embed.WithTitle(config.PanelTitle ?? "Self Roles");

        if (!string.IsNullOrWhiteSpace(config.PanelColorHex) && TryParseColor(config.PanelColorHex, out var color))
        {
            embed.WithColor(color);
        }
        else
        {
            embed.WithColor(SectorUI.SectorPrimary);
        }

        if (!string.IsNullOrWhiteSpace(config.PanelThumbnailUrl) && Uri.TryCreate(config.PanelThumbnailUrl, UriKind.Absolute, out var thumbnailUri))
        {
            embed.WithThumbnail(thumbnailUri);
        }

        if (!string.IsNullOrWhiteSpace(config.PanelImageUrl) && Uri.TryCreate(config.PanelImageUrl, UriKind.Absolute, out var imageUri))
        {
            embed.WithImageUrl(imageUri);
        }

        if (!string.IsNullOrWhiteSpace(config.PanelFooter))
        {
            embed.WithFooter(config.PanelFooter);
        }

        var totalRoles = options.Count.ToString();
        var moderation = config.RequireModeration ? "Moderated" : "Immediate";
        var allowMultiple = config.AllowMultipleRoles ? "yes" : "no";

        var defaultTemplate = "{groups}\n{options}";
        var template = string.IsNullOrWhiteSpace(config.PanelDescriptionTemplate) ? defaultTemplate : config.PanelDescriptionTemplate;

        var groupBlocks = new StringBuilder();
        var groupedOptionIds = new HashSet<int>();

        foreach (var group in groups)
        {
            var groupOptions = options.Where(x => x.GroupId == group.Id).ToList();
            if (groupOptions.Count == 0)
            {
                continue;
            }

            groupBlocks.AppendLine($"**{group.Name}**{(group.IsExclusive ? " (exclusive)" : string.Empty)}");
            foreach (var option in groupOptions)
            {
                groupedOptionIds.Add(option.Id);
                var roleName = guild.Roles.GetValueOrDefault(option.RoleId)?.Name ?? option.Label;
                groupBlocks.AppendLine(RenderOptionLine(option, roleName, group.Name, totalRoles, allowMultiple, moderation));
            }

            groupBlocks.AppendLine();
        }

        var ungrouped = options.Where(x => !groupedOptionIds.Contains(x.Id)).ToList();
        var optionBlocks = new StringBuilder();
        foreach (var option in ungrouped)
        {
            var roleName = guild.Roles.GetValueOrDefault(option.RoleId)?.Name ?? option.Label;
            optionBlocks.AppendLine(RenderOptionLine(option, roleName, string.Empty, totalRoles, allowMultiple, moderation));
        }

        var description = BotPromptTokens.ApplySelfRoleTemplate(
            template,
            emoji: string.Empty,
            label: string.Empty,
            roleName: string.Empty,
            roleMention: string.Empty,
            description: string.Empty,
            groupName: string.Empty,
            displayOrder: string.Empty,
            totalRoles: totalRoles,
            allowMultiple: allowMultiple,
            moderation: moderation)
            .Replace("{groups}", groupBlocks.ToString().Trim())
            .Replace("{options}", optionBlocks.ToString().Trim())
            .Trim();

        embed.WithDescription(string.IsNullOrWhiteSpace(description) ? "React to choose a role." : description);
        return embed;
    }

    private static string RenderOptionLine(SelfRoleOption option, string roleName, string groupName, string totalRoles, string allowMultiple, string moderation)
    {
        return BotPromptTokens.ApplySelfRoleTemplate(
            "{emoji} **{label}** {role_mention} - {description}",
            option.EmojiKey,
            option.Label,
            roleName,
            $"<@&{option.RoleId}>",
            option.Description ?? string.Empty,
            groupName,
            option.DisplayOrder.ToString(),
            totalRoles,
            allowMultiple,
            moderation);
    }

    private async Task SyncPanelReactionsAsync(DiscordClient client, DiscordMessage message, IReadOnlyCollection<SelfRoleOption> options)
    {
        foreach (var option in options.OrderBy(x => x.DisplayOrder).ThenBy(x => x.Id))
        {
            if (TryCreateEmoji(client, option.EmojiKey, out var emoji))
            {
                try
                {
                    await message.CreateReactionAsync(emoji);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to add reaction {Emoji} to self-role panel message {MessageId}", option.EmojiKey, message.Id);
                }
            }
        }
    }

    private static string NormalizeEmojiKey(DiscordClient client, string input)
    {
        if (TryCreateEmoji(client, input, out var emoji))
        {
            return emoji.GetDiscordName();
        }

        return input.Trim();
    }

    private static bool TryCreateEmoji(DiscordClient client, string input, out DiscordEmoji emoji)
    {
        emoji = default!;
        input = input.Trim();

        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        if (input.StartsWith("<:", StringComparison.Ordinal) || input.StartsWith("<a:", StringComparison.Ordinal))
        {
            var lastColon = input.LastIndexOf(':');
            if (lastColon > 0 && input.EndsWith('>') && ulong.TryParse(input.Substring(lastColon + 1, input.Length - lastColon - 2), out var emojiId))
            {
                emoji = DiscordEmoji.FromGuildEmote(client, emojiId);
                return true;
            }
        }

        if (ulong.TryParse(input, out var customEmojiId))
        {
            emoji = DiscordEmoji.FromGuildEmote(client, customEmojiId);
            return true;
        }

        try
        {
            emoji = DiscordEmoji.FromUnicode(input);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryParseColor(string value, out DiscordColor color)
    {
        color = default;
        value = value.Trim();

        if (value.StartsWith('#'))
        {
            value = value[1..];
        }

        if (value.Length != 6 || !int.TryParse(value, System.Globalization.NumberStyles.HexNumber, null, out var rgb))
        {
            return false;
        }

        color = new DiscordColor(rgb);
        return true;
    }
}