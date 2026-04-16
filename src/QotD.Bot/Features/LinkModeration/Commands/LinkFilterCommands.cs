using System.ComponentModel;
using DSharpPlus;
using DSharpPlus.Commands;
using DSharpPlus.Commands.ContextChecks;
using DSharpPlus.Entities;
using Microsoft.EntityFrameworkCore;
using QotD.Bot.Data;
using QotD.Bot.Data.Models;
using QotD.Bot.Features.LinkModeration.Services;

namespace QotD.Bot.Features.LinkModeration.Commands;

[Command("linkfilter")]
[Description("Configure automatic link moderation")]
public sealed class LinkFilterCommands
{
    private readonly AppDbContext _db;
    private readonly LinkModerationService _service;

    public LinkFilterCommands(AppDbContext db, LinkModerationService service)
    {
        _db = db;
        _service = service;
    }

    [Command("status")]
    [RequirePermissions(DiscordPermission.ManageGuild)]
    [Description("Show current link filter settings")]
    public async ValueTask StatusAsync(CommandContext ctx)
    {
        if (ctx.Guild is null)
        {
            await ctx.RespondAsync("Dieser Befehl kann nur in einem Server genutzt werden.");
            return;
        }

        var config = await _db.LinkFilterConfigs.AsNoTracking().FirstOrDefaultAsync(x => x.GuildId == ctx.Guild.Id);
        var rules = await _service.GetRulesAsync(ctx.Guild.Id);
        var bypassRoles = await _service.GetBypassRolesAsync(ctx.Guild.Id);
        var bypassChannels = await _service.GetBypassChannelsAsync(ctx.Guild.Id);

        if (config is null)
        {
            await ctx.RespondAsync("Linkfilter ist noch nicht eingerichtet. Nutze `/linkfilter enable` um zu starten.");
            return;
        }

        var modeText = config.Mode == LinkFilterMode.Whitelist ? "Whitelist" : "Blacklist";
        var roleText = bypassRoles.Count == 0 ? "none" : string.Join(", ", bypassRoles.Select(x => $"<@&{x.RoleId}>"));
        var channelText = bypassChannels.Count == 0 ? "none" : string.Join(", ", bypassChannels.Select(x => $"<#{x.ChannelId}>"));

        await ctx.RespondAsync(
            $"Linkfilter status:\n" +
            $"- Enabled: {(config.IsEnabled ? "yes" : "no")}\n" +
            $"- Mode: {modeText}\n" +
            $"- Log channel: {(config.LogChannelId is > 0 ? $"<#{config.LogChannelId}>" : "not set")}\n" +
            $"- DM warning: {(config.SendDirectMessageWarning ? "on" : "off")}\n" +
            $"- Channel warning: {(config.SendChannelWarning ? "on" : "off")}\n" +
            $"- Rules: {rules.Count}\n" +
            $"- Bypass roles: {roleText}\n" +
            $"- Bypass channels: {channelText}");
    }

    [Command("enable")]
    [RequirePermissions(DiscordPermission.ManageGuild)]
    [Description("Enable link filtering")]
    public async ValueTask EnableAsync(CommandContext ctx)
    {
        if (ctx.Guild is null)
        {
            await ctx.RespondAsync("Dieser Befehl kann nur in einem Server genutzt werden.");
            return;
        }

        var config = await _service.GetOrCreateConfigAsync(ctx.Guild.Id);
        config.IsEnabled = true;
        await _db.SaveChangesAsync();

        await ctx.RespondAsync("✅ Linkfilter wurde aktiviert.");
    }

    [Command("disable")]
    [RequirePermissions(DiscordPermission.ManageGuild)]
    [Description("Disable link filtering")]
    public async ValueTask DisableAsync(CommandContext ctx)
    {
        if (ctx.Guild is null)
        {
            await ctx.RespondAsync("Dieser Befehl kann nur in einem Server genutzt werden.");
            return;
        }

        var config = await _service.GetOrCreateConfigAsync(ctx.Guild.Id);
        config.IsEnabled = false;
        await _db.SaveChangesAsync();

        await ctx.RespondAsync("✅ Linkfilter wurde deaktiviert.");
    }

    [Command("mode")]
    [RequirePermissions(DiscordPermission.ManageGuild)]
    [Description("Set mode: whitelist or blacklist")]
    public async ValueTask ModeAsync(CommandContext ctx, [Description("whitelist or blacklist")] string mode)
    {
        if (ctx.Guild is null)
        {
            await ctx.RespondAsync("Dieser Befehl kann nur in einem Server genutzt werden.");
            return;
        }

        var normalized = mode.Trim().ToLowerInvariant();
        LinkFilterMode parsedMode;

        if (normalized is "whitelist" or "white")
        {
            parsedMode = LinkFilterMode.Whitelist;
        }
        else if (normalized is "blacklist" or "black")
        {
            parsedMode = LinkFilterMode.Blacklist;
        }
        else
        {
            await ctx.RespondAsync("❌ Ungueltiger Modus. Nutze `whitelist` oder `blacklist`.");
            return;
        }

        var config = await _service.GetOrCreateConfigAsync(ctx.Guild.Id);
        config.Mode = parsedMode;
        await _db.SaveChangesAsync();

        await ctx.RespondAsync($"✅ Linkfilter-Modus wurde auf **{parsedMode}** gesetzt.");
    }

    [Command("logchannel")]
    [RequirePermissions(DiscordPermission.ManageGuild)]
    [Description("Set the log channel (omit channel to clear)")]
    public async ValueTask LogChannelAsync(CommandContext ctx, [Description("Target log channel")] DiscordChannel? channel = null)
    {
        if (ctx.Guild is null)
        {
            await ctx.RespondAsync("Dieser Befehl kann nur in einem Server genutzt werden.");
            return;
        }

        var config = await _service.GetOrCreateConfigAsync(ctx.Guild.Id);
        config.LogChannelId = channel?.Id;
        await _db.SaveChangesAsync();

        if (channel is null)
        {
            await ctx.RespondAsync("✅ Log-Channel wurde entfernt.");
            return;
        }

        await ctx.RespondAsync($"✅ Log-Channel gesetzt auf {channel.Mention}.");
    }

    [Command("dmwarn")]
    [RequirePermissions(DiscordPermission.ManageGuild)]
    [Description("Enable or disable DM warning")]
    public async ValueTask DmWarnAsync(CommandContext ctx, [Description("true or false")] bool enabled)
    {
        if (ctx.Guild is null)
        {
            await ctx.RespondAsync("Dieser Befehl kann nur in einem Server genutzt werden.");
            return;
        }

        var config = await _service.GetOrCreateConfigAsync(ctx.Guild.Id);
        config.SendDirectMessageWarning = enabled;
        await _db.SaveChangesAsync();

        await ctx.RespondAsync(enabled
            ? "✅ DM-Warnung aktiviert."
            : "✅ DM-Warnung deaktiviert.");
    }

    [Command("channelwarn")]
    [RequirePermissions(DiscordPermission.ManageGuild)]
    [Description("Enable or disable in-channel warning")]
    public async ValueTask ChannelWarnAsync(CommandContext ctx, [Description("true or false")] bool enabled)
    {
        if (ctx.Guild is null)
        {
            await ctx.RespondAsync("Dieser Befehl kann nur in einem Server genutzt werden.");
            return;
        }

        var config = await _service.GetOrCreateConfigAsync(ctx.Guild.Id);
        config.SendChannelWarning = enabled;
        await _db.SaveChangesAsync();

        await ctx.RespondAsync(enabled
            ? "✅ Kanal-Warnung aktiviert."
            : "✅ Kanal-Warnung deaktiviert.");
    }

    [Command("ruleadd")]
    [RequirePermissions(DiscordPermission.ManageGuild)]
    [Description("Add a domain rule")]
    public async ValueTask RuleAddAsync(CommandContext ctx, [Description("Domain, for example example.com")] string domain)
    {
        if (ctx.Guild is null)
        {
            await ctx.RespondAsync("Dieser Befehl kann nur in einem Server genutzt werden.");
            return;
        }

        var normalized = LinkModerationService.NormalizeDomain(domain);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            await ctx.RespondAsync("❌ Ungueltige Domain.");
            return;
        }

        var exists = await _db.LinkFilterRules
            .AnyAsync(x => x.GuildId == ctx.Guild.Id && x.NormalizedDomain == normalized);

        if (exists)
        {
            await ctx.RespondAsync($"ℹ️ Domain `{normalized}` ist bereits vorhanden.");
            return;
        }

        _db.LinkFilterRules.Add(new LinkFilterRule
        {
            GuildId = ctx.Guild.Id,
            NormalizedDomain = normalized
        });

        await _db.SaveChangesAsync();
        await ctx.RespondAsync($"✅ Domain `{normalized}` hinzugefuegt.");
    }

    [Command("ruleremove")]
    [RequirePermissions(DiscordPermission.ManageGuild)]
    [Description("Remove a domain rule")]
    public async ValueTask RuleRemoveAsync(CommandContext ctx, [Description("Domain to remove")] string domain)
    {
        if (ctx.Guild is null)
        {
            await ctx.RespondAsync("Dieser Befehl kann nur in einem Server genutzt werden.");
            return;
        }

        var normalized = LinkModerationService.NormalizeDomain(domain);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            await ctx.RespondAsync("❌ Ungueltige Domain.");
            return;
        }

        var rule = await _db.LinkFilterRules
            .FirstOrDefaultAsync(x => x.GuildId == ctx.Guild.Id && x.NormalizedDomain == normalized);

        if (rule is null)
        {
            await ctx.RespondAsync("ℹ️ Diese Domain ist nicht in der Liste.");
            return;
        }

        _db.LinkFilterRules.Remove(rule);
        await _db.SaveChangesAsync();
        await ctx.RespondAsync($"✅ Domain `{normalized}` entfernt.");
    }

    [Command("rules")]
    [RequirePermissions(DiscordPermission.ManageGuild)]
    [Description("List all domain rules")]
    public async ValueTask RulesAsync(CommandContext ctx)
    {
        if (ctx.Guild is null)
        {
            await ctx.RespondAsync("Dieser Befehl kann nur in einem Server genutzt werden.");
            return;
        }

        var rules = await _service.GetRulesAsync(ctx.Guild.Id);
        if (rules.Count == 0)
        {
            await ctx.RespondAsync("Keine Regeln konfiguriert.");
            return;
        }

        var text = string.Join("\n", rules.Select(x => $"- `{x.NormalizedDomain}`"));
        await ctx.RespondAsync($"Aktuelle Domain-Regeln:\n{text}");
    }

    [Command("bypassroleadd")]
    [RequirePermissions(DiscordPermission.ManageGuild)]
    [Description("Allow a role to bypass link filtering")]
    public async ValueTask BypassRoleAddAsync(CommandContext ctx, [Description("Role to bypass filtering")] DiscordRole role)
    {
        if (ctx.Guild is null)
        {
            await ctx.RespondAsync("Dieser Befehl kann nur in einem Server genutzt werden.");
            return;
        }

        var exists = await _db.LinkFilterBypassRoles.AnyAsync(x => x.GuildId == ctx.Guild.Id && x.RoleId == role.Id);
        if (exists)
        {
            await ctx.RespondAsync("ℹ️ Rolle ist bereits in der Bypass-Liste.");
            return;
        }

        _db.LinkFilterBypassRoles.Add(new LinkFilterBypassRole
        {
            GuildId = ctx.Guild.Id,
            RoleId = role.Id
        });

        await _db.SaveChangesAsync();
        await ctx.RespondAsync($"✅ Rolle {role.Mention} als Bypass hinzugefuegt.");
    }

    [Command("bypassroleremove")]
    [RequirePermissions(DiscordPermission.ManageGuild)]
    [Description("Remove a bypass role")]
    public async ValueTask BypassRoleRemoveAsync(CommandContext ctx, [Description("Role to remove")] DiscordRole role)
    {
        if (ctx.Guild is null)
        {
            await ctx.RespondAsync("Dieser Befehl kann nur in einem Server genutzt werden.");
            return;
        }

        var entry = await _db.LinkFilterBypassRoles.FirstOrDefaultAsync(x => x.GuildId == ctx.Guild.Id && x.RoleId == role.Id);
        if (entry is null)
        {
            await ctx.RespondAsync("ℹ️ Rolle ist nicht in der Bypass-Liste.");
            return;
        }

        _db.LinkFilterBypassRoles.Remove(entry);
        await _db.SaveChangesAsync();
        await ctx.RespondAsync($"✅ Rolle {role.Mention} aus Bypass-Liste entfernt.");
    }

    [Command("bypassroles")]
    [RequirePermissions(DiscordPermission.ManageGuild)]
    [Description("List bypass roles")]
    public async ValueTask BypassRolesAsync(CommandContext ctx)
    {
        if (ctx.Guild is null)
        {
            await ctx.RespondAsync("Dieser Befehl kann nur in einem Server genutzt werden.");
            return;
        }

        var roles = await _service.GetBypassRolesAsync(ctx.Guild.Id);
        if (roles.Count == 0)
        {
            await ctx.RespondAsync("Keine Bypass-Rollen konfiguriert.");
            return;
        }

        var text = string.Join("\n", roles.Select(x => $"- <@&{x.RoleId}>"));
        await ctx.RespondAsync($"Bypass-Rollen:\n{text}");
    }

    [Command("bypasschanneladd")]
    [RequirePermissions(DiscordPermission.ManageGuild)]
    [Description("Allow a channel to bypass link filtering")]
    public async ValueTask BypassChannelAddAsync(CommandContext ctx, [Description("Channel to bypass filtering")] DiscordChannel channel)
    {
        if (ctx.Guild is null)
        {
            await ctx.RespondAsync("Dieser Befehl kann nur in einem Server genutzt werden.");
            return;
        }

        var exists = await _db.LinkFilterBypassChannels.AnyAsync(x => x.GuildId == ctx.Guild.Id && x.ChannelId == channel.Id);
        if (exists)
        {
            await ctx.RespondAsync("ℹ️ Kanal ist bereits in der Bypass-Liste.");
            return;
        }

        _db.LinkFilterBypassChannels.Add(new LinkFilterBypassChannel
        {
            GuildId = ctx.Guild.Id,
            ChannelId = channel.Id
        });

        await _db.SaveChangesAsync();
        await ctx.RespondAsync($"✅ Kanal {channel.Mention} als Bypass hinzugefuegt.");
    }

    [Command("bypasschannelremove")]
    [RequirePermissions(DiscordPermission.ManageGuild)]
    [Description("Remove a bypass channel")]
    public async ValueTask BypassChannelRemoveAsync(CommandContext ctx, [Description("Channel to remove")] DiscordChannel channel)
    {
        if (ctx.Guild is null)
        {
            await ctx.RespondAsync("Dieser Befehl kann nur in einem Server genutzt werden.");
            return;
        }

        var entry = await _db.LinkFilterBypassChannels.FirstOrDefaultAsync(x => x.GuildId == ctx.Guild.Id && x.ChannelId == channel.Id);
        if (entry is null)
        {
            await ctx.RespondAsync("ℹ️ Kanal ist nicht in der Bypass-Liste.");
            return;
        }

        _db.LinkFilterBypassChannels.Remove(entry);
        await _db.SaveChangesAsync();
        await ctx.RespondAsync($"✅ Kanal {channel.Mention} aus Bypass-Liste entfernt.");
    }

    [Command("bypasschannels")]
    [RequirePermissions(DiscordPermission.ManageGuild)]
    [Description("List bypass channels")]
    public async ValueTask BypassChannelsAsync(CommandContext ctx)
    {
        if (ctx.Guild is null)
        {
            await ctx.RespondAsync("Dieser Befehl kann nur in einem Server genutzt werden.");
            return;
        }

        var channels = await _service.GetBypassChannelsAsync(ctx.Guild.Id);
        if (channels.Count == 0)
        {
            await ctx.RespondAsync("Keine Bypass-Kanaele konfiguriert.");
            return;
        }

        var text = string.Join("\n", channels.Select(x => $"- <#{x.ChannelId}>"));
        await ctx.RespondAsync($"Bypass-Kanaele:\n{text}");
    }
}
