using System.ComponentModel;
using DSharpPlus;
using DSharpPlus.Commands;
using DSharpPlus.Commands.ContextChecks;
using DSharpPlus.Entities;
using QotD.Bot.Data.Models;
using QotD.Bot.Features.AutoModeration.Services;

namespace QotD.Bot.Features.AutoModeration.Commands;

[Command("automod")]
[Description("Configure raid mode, age gates and audit trail")]
public sealed class AutoModerationCommands
{
    private readonly AutoModerationService _service;

    public AutoModerationCommands(AutoModerationService service)
    {
        _service = service;
    }

    [Command("status")]
    [RequirePermissions(DiscordPermission.ManageGuild)]
    [Description("Show current auto moderation configuration")]
    public async ValueTask StatusAsync(CommandContext ctx)
    {
        if (ctx.Guild is null)
        {
            await ctx.RespondAsync("Dieser Befehl kann nur in einem Server genutzt werden.");
            return;
        }

        var config = await _service.GetOrCreateConfigAsync(ctx.Guild.Id);

        var verifiedRoleText = config.VerifiedRoleId is > 0 ? $"<@&{config.VerifiedRoleId}>" : "not set";
        var logChannelText = config.LogChannelId is > 0 ? $"<#{config.LogChannelId}>" : "not set";

        await ctx.RespondAsync(
            "AutoMod status:\n" +
            $"- Enabled: {(config.IsEnabled ? "yes" : "no")}\n" +
            $"- Raid mode: {(config.RaidModeEnabled ? "on" : "off")}\n" +
            $"- Raid threshold: {config.RaidJoinThreshold} joins / {config.RaidWindowSeconds}s\n" +
            $"- Lockdown duration: {config.RaidLockdownMinutes} min\n" +
            $"- Lockdown active: {(config.IsLockdownActive ? "yes" : "no")}\n" +
            $"- Lockdown verified role only: {(config.RestrictToVerifiedRoleDuringLockdown ? "yes" : "no")}\n" +
            $"- Verified role: {verifiedRoleText}\n" +
            $"- Lockdown min account age: {config.LockdownMinAccountAgeHours}h\n" +
            $"- Link gate account age: {(config.EnforceAccountAgeForLinks ? "on" : "off")} ({config.MinAccountAgeDaysForLinks} days)\n" +
            $"- Link gate server age: {(config.EnforceServerAgeForLinks ? "on" : "off")} ({config.MinServerAgeHoursForLinks}h)\n" +
            $"- Log channel: {logChannelText}");
    }

    [Command("enable")]
    [RequirePermissions(DiscordPermission.ManageGuild)]
    [Description("Enable auto moderation")]
    public async ValueTask EnableAsync(CommandContext ctx)
    {
        if (ctx.Guild is null)
        {
            await ctx.RespondAsync("Dieser Befehl kann nur in einem Server genutzt werden.");
            return;
        }

        var config = await _service.GetOrCreateConfigAsync(ctx.Guild.Id);
        config.IsEnabled = true;
        config.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await _service.AddAuditEntryAsync(new()
        {
            GuildId = ctx.Guild.Id,
            UserId = ctx.User.Id,
            Action = AutoModerationAuditAction.ConfigChanged,
            RuleKey = "config.enabled",
            Reason = "Auto moderation enabled by command."
        });

        await ctx.RespondAsync("✅ AutoMod wurde aktiviert.");
    }

    [Command("disable")]
    [RequirePermissions(DiscordPermission.ManageGuild)]
    [Description("Disable auto moderation")]
    public async ValueTask DisableAsync(CommandContext ctx)
    {
        if (ctx.Guild is null)
        {
            await ctx.RespondAsync("Dieser Befehl kann nur in einem Server genutzt werden.");
            return;
        }

        var config = await _service.GetOrCreateConfigAsync(ctx.Guild.Id);
        config.IsEnabled = false;
        config.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await _service.AddAuditEntryAsync(new()
        {
            GuildId = ctx.Guild.Id,
            UserId = ctx.User.Id,
            Action = AutoModerationAuditAction.ConfigChanged,
            RuleKey = "config.disabled",
            Reason = "Auto moderation disabled by command."
        });

        await ctx.RespondAsync("✅ AutoMod wurde deaktiviert.");
    }

    [Command("logchannel")]
    [RequirePermissions(DiscordPermission.ManageGuild)]
    [Description("Set or clear automod log channel")]
    public async ValueTask LogChannelAsync(CommandContext ctx, [Description("target log channel")] DiscordChannel? channel = null)
    {
        if (ctx.Guild is null)
        {
            await ctx.RespondAsync("Dieser Befehl kann nur in einem Server genutzt werden.");
            return;
        }

        var config = await _service.GetOrCreateConfigAsync(ctx.Guild.Id);
        config.LogChannelId = channel?.Id;
        config.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await _service.AddAuditEntryAsync(new()
        {
            GuildId = ctx.Guild.Id,
            UserId = ctx.User.Id,
            Action = AutoModerationAuditAction.ConfigChanged,
            RuleKey = "config.logchannel",
            Reason = channel is null ? "Auto moderation log channel cleared." : $"Auto moderation log channel set to {channel.Id}."
        });

        await ctx.RespondAsync(channel is null
            ? "✅ AutoMod Log-Channel entfernt."
            : $"✅ AutoMod Log-Channel gesetzt auf {channel.Mention}.");
    }

    [Command("raidsettings")]
    [RequirePermissions(DiscordPermission.ManageGuild)]
    [Description("Configure raid threshold and lockdown duration")]
    public async ValueTask RaidSettingsAsync(
        CommandContext ctx,
        [Description("joins required to trigger lockdown")] int joinThreshold,
        [Description("time window in seconds")] int windowSeconds,
        [Description("lockdown duration in minutes")] int lockdownMinutes)
    {
        if (ctx.Guild is null)
        {
            await ctx.RespondAsync("Dieser Befehl kann nur in einem Server genutzt werden.");
            return;
        }

        if (joinThreshold < 2 || windowSeconds < 10 || lockdownMinutes < 1)
        {
            await ctx.RespondAsync("❌ Ungueltige Werte. Mindestwerte: threshold>=2, window>=10s, duration>=1m.");
            return;
        }

        var config = await _service.GetOrCreateConfigAsync(ctx.Guild.Id);
        config.RaidJoinThreshold = joinThreshold;
        config.RaidWindowSeconds = windowSeconds;
        config.RaidLockdownMinutes = lockdownMinutes;
        config.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await _service.AddAuditEntryAsync(new()
        {
            GuildId = ctx.Guild.Id,
            UserId = ctx.User.Id,
            Action = AutoModerationAuditAction.ConfigChanged,
            RuleKey = "config.raid",
            Reason = $"Raid settings updated: threshold={joinThreshold}, window={windowSeconds}s, duration={lockdownMinutes}m."
        });

        await ctx.RespondAsync("✅ Raid-Einstellungen aktualisiert.");
    }

    [Command("raidmode")]
    [RequirePermissions(DiscordPermission.ManageGuild)]
    [Description("Enable or disable automatic raid detection")]
    public async ValueTask RaidModeAsync(CommandContext ctx, [Description("true or false")] bool enabled)
    {
        if (ctx.Guild is null)
        {
            await ctx.RespondAsync("Dieser Befehl kann nur in einem Server genutzt werden.");
            return;
        }

        var config = await _service.GetOrCreateConfigAsync(ctx.Guild.Id);
        config.RaidModeEnabled = enabled;
        config.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await _service.AddAuditEntryAsync(new()
        {
            GuildId = ctx.Guild.Id,
            UserId = ctx.User.Id,
            Action = AutoModerationAuditAction.ConfigChanged,
            RuleKey = "config.raidmode",
            Reason = enabled ? "Raid mode enabled." : "Raid mode disabled."
        });

        await ctx.RespondAsync(enabled
            ? "✅ Raid-Mode aktiviert."
            : "✅ Raid-Mode deaktiviert.");
    }

    [Command("lockdown")]
    [RequirePermissions(DiscordPermission.ManageGuild)]
    [Description("Manually enable or disable lockdown")]
    public async ValueTask LockdownAsync(
        CommandContext ctx,
        [Description("true or false")] bool enabled,
        [Description("duration in minutes when enabled")] int minutes = 30)
    {
        if (ctx.Guild is null)
        {
            await ctx.RespondAsync("Dieser Befehl kann nur in einem Server genutzt werden.");
            return;
        }

        if (enabled && minutes < 1)
        {
            await ctx.RespondAsync("❌ Dauer muss mindestens 1 Minute sein.");
            return;
        }

        var config = await _service.GetOrCreateConfigAsync(ctx.Guild.Id);
        var now = DateTimeOffset.UtcNow;

        if (enabled)
        {
            config.IsLockdownActive = true;
            config.LockdownActivatedAtUtc = now;
            config.LockdownEndsAtUtc = now.AddMinutes(minutes);
            config.UpdatedAtUtc = now;

            await _service.AddAuditEntryAsync(new()
            {
                GuildId = ctx.Guild.Id,
                UserId = ctx.User.Id,
                Action = AutoModerationAuditAction.LockdownActivated,
                RuleKey = "raid.lockdown.manual",
                Reason = $"Manual lockdown enabled for {minutes} minute(s)."
            });

            await ctx.RespondAsync($"✅ Lockdown aktiviert fuer {minutes} Minute(n).");
            return;
        }

        config.IsLockdownActive = false;
        config.LockdownActivatedAtUtc = null;
        config.LockdownEndsAtUtc = null;
        config.UpdatedAtUtc = now;

        await _service.AddAuditEntryAsync(new()
        {
            GuildId = ctx.Guild.Id,
            UserId = ctx.User.Id,
            Action = AutoModerationAuditAction.LockdownEnded,
            RuleKey = "raid.lockdown.manual",
            Reason = "Manual lockdown disabled."
        });

        await ctx.RespondAsync("✅ Lockdown deaktiviert.");
    }

    [Command("verifiedrole")]
    [RequirePermissions(DiscordPermission.ManageGuild)]
    [Description("Set verified role used during lockdown; omit to clear")]
    public async ValueTask VerifiedRoleAsync(CommandContext ctx, [Description("verified role")] DiscordRole? role = null)
    {
        if (ctx.Guild is null)
        {
            await ctx.RespondAsync("Dieser Befehl kann nur in einem Server genutzt werden.");
            return;
        }

        var config = await _service.GetOrCreateConfigAsync(ctx.Guild.Id);
        config.VerifiedRoleId = role?.Id;
        config.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await _service.AddAuditEntryAsync(new()
        {
            GuildId = ctx.Guild.Id,
            UserId = ctx.User.Id,
            Action = AutoModerationAuditAction.ConfigChanged,
            RuleKey = "config.verified-role",
            Reason = role is null ? "Verified role cleared." : $"Verified role set to {role.Id}."
        });

        await ctx.RespondAsync(role is null
            ? "✅ Verified-Rolle entfernt."
            : $"✅ Verified-Rolle gesetzt auf {role.Mention}.");
    }

    [Command("lockdownrules")]
    [RequirePermissions(DiscordPermission.ManageGuild)]
    [Description("Configure lockdown posting rules")]
    public async ValueTask LockdownRulesAsync(
        CommandContext ctx,
        [Description("only verified role can post during lockdown")] bool verifiedOnly,
        [Description("minimum account age in hours during lockdown")] int minAccountAgeHours)
    {
        if (ctx.Guild is null)
        {
            await ctx.RespondAsync("Dieser Befehl kann nur in einem Server genutzt werden.");
            return;
        }

        if (minAccountAgeHours < 0)
        {
            await ctx.RespondAsync("❌ minAccountAgeHours darf nicht negativ sein.");
            return;
        }

        var config = await _service.GetOrCreateConfigAsync(ctx.Guild.Id);
        config.RestrictToVerifiedRoleDuringLockdown = verifiedOnly;
        config.LockdownMinAccountAgeHours = minAccountAgeHours;
        config.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await _service.AddAuditEntryAsync(new()
        {
            GuildId = ctx.Guild.Id,
            UserId = ctx.User.Id,
            Action = AutoModerationAuditAction.ConfigChanged,
            RuleKey = "config.lockdown-rules",
            Reason = $"Lockdown rules updated: verifiedOnly={verifiedOnly}, minAccountAgeHours={minAccountAgeHours}."
        });

        await ctx.RespondAsync("✅ Lockdown-Regeln aktualisiert.");
    }

    [Command("gates")]
    [RequirePermissions(DiscordPermission.ManageGuild)]
    [Description("Configure account/server age gates for posting links")]
    public async ValueTask GatesAsync(
        CommandContext ctx,
        [Description("enforce account age gate")] bool enforceAccountAge,
        [Description("minimum account age in days")] int minAccountAgeDays,
        [Description("enforce server age gate")] bool enforceServerAge,
        [Description("minimum server age in hours")] int minServerAgeHours)
    {
        if (ctx.Guild is null)
        {
            await ctx.RespondAsync("Dieser Befehl kann nur in einem Server genutzt werden.");
            return;
        }

        if (minAccountAgeDays < 0 || minServerAgeHours < 0)
        {
            await ctx.RespondAsync("❌ Mindestalter darf nicht negativ sein.");
            return;
        }

        var config = await _service.GetOrCreateConfigAsync(ctx.Guild.Id);
        config.EnforceAccountAgeForLinks = enforceAccountAge;
        config.MinAccountAgeDaysForLinks = minAccountAgeDays;
        config.EnforceServerAgeForLinks = enforceServerAge;
        config.MinServerAgeHoursForLinks = minServerAgeHours;
        config.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await _service.AddAuditEntryAsync(new()
        {
            GuildId = ctx.Guild.Id,
            UserId = ctx.User.Id,
            Action = AutoModerationAuditAction.ConfigChanged,
            RuleKey = "config.gates",
            Reason = $"Gate rules updated: account({enforceAccountAge}/{minAccountAgeDays}d), server({enforceServerAge}/{minServerAgeHours}h)."
        });

        await ctx.RespondAsync("✅ Account-/Server-Age-Gates aktualisiert.");
    }

    [Command("audit")]
    [RequirePermissions(DiscordPermission.ManageGuild)]
    [Description("Show recent auto moderation audit entries")]
    public async ValueTask AuditAsync(CommandContext ctx, [Description("number of entries (1-50)")] int count = 10)
    {
        if (ctx.Guild is null)
        {
            await ctx.RespondAsync("Dieser Befehl kann nur in einem Server genutzt werden.");
            return;
        }

        var entries = await _service.GetRecentAuditEntriesAsync(ctx.Guild.Id, count);
        if (entries.Count == 0)
        {
            await ctx.RespondAsync("Keine AutoMod-Audit-Eintraege vorhanden.");
            return;
        }

        var lines = entries.Select(x =>
            $"- {x.CreatedAtUtc:yyyy-MM-dd HH:mm} | {x.Action} | {x.RuleKey} | {(x.UserId is > 0 ? $"<@{x.UserId}>" : "n/a")}");

        await ctx.RespondAsync("Letzte AutoMod-Audit-Eintraege:\n" + string.Join("\n", lines));
    }
}
