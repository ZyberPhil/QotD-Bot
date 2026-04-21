using System.ComponentModel;
using DSharpPlus;
using DSharpPlus.Commands;
using DSharpPlus.Commands.ContextChecks;
using DSharpPlus.Entities;
using QotD.Bot.Features.Teams.Services;
using QotD.Bot.UI;

namespace QotD.Bot.Features.Teams.Commands;

[Command("ban")]
[Description("Moderations-Befehle fuer User-Bans und interne IP-Sperrliste")]
public sealed class ModerationCommand
{
    private readonly ModerationService _moderationService;

    public ModerationCommand(ModerationService moderationService)
    {
        _moderationService = moderationService;
    }

    [Command("user")]
    [Description("Bannt ein Mitglied vom Server.")]
    [RequirePermissions(DiscordPermission.ManageGuild)]
    public async ValueTask BanUserAsync(
        CommandContext ctx,
        [Description("User")] DiscordUser user,
        [Description("Grund")] string reason = "Kein Grund angegeben")
    {
        if (ctx.Guild is null)
        {
            await ctx.RespondAsync("Dieser Befehl kann nur in einem Server genutzt werden.");
            return;
        }

        try
        {
            await _moderationService.BanUserAsync(ctx.Guild, user, ctx.User.Id, reason);
        }
        catch (Exception ex)
        {
            await ctx.RespondAsync($"Ban fehlgeschlagen: {ex.Message}");
            return;
        }

        var embed = new DiscordEmbedBuilder()
            .WithFeatureTitle("Moderation", "User gebannt", "🔨")
            .WithColor(SectorUI.SectorDanger)
            .WithDescription($"{user.Mention} wurde gebannt.\nGrund: **{reason}**")
            .WithTimestamp(DateTimeOffset.UtcNow);

        await ctx.RespondAsync(new DiscordMessageBuilder().AddEmbed(embed));
    }

    [Command("unban")]
    [Description("Entbannt einen User ueber seine User-ID.")]
    [RequirePermissions(DiscordPermission.ManageGuild)]
    public async ValueTask UnbanUserAsync(
        CommandContext ctx,
        [Description("User-ID")] string userId,
        [Description("Grund")] string reason = "Kein Grund angegeben")
    {
        if (ctx.Guild is null)
        {
            await ctx.RespondAsync("Dieser Befehl kann nur in einem Server genutzt werden.");
            return;
        }

        if (!ulong.TryParse(userId.Trim(), out var parsedUserId))
        {
            await ctx.RespondAsync("Bitte gib eine gueltige numerische User-ID an.");
            return;
        }

        try
        {
            await _moderationService.UnbanUserAsync(ctx.Guild, parsedUserId, reason);
        }
        catch (Exception ex)
        {
            await ctx.RespondAsync($"Unban fehlgeschlagen: {ex.Message}");
            return;
        }

        var embed = new DiscordEmbedBuilder()
            .WithFeatureTitle("Moderation", "User entbannt", "✅")
            .WithColor(SectorUI.SectorSuccessGreen)
            .WithDescription($"User mit ID **{parsedUserId}** wurde entbannt.\nGrund: **{reason}**")
            .WithTimestamp(DateTimeOffset.UtcNow);

        await ctx.RespondAsync(new DiscordMessageBuilder().AddEmbed(embed));
    }

    [Command("ip")]
    [Description("Interne IP-Sperrliste verwalten (nicht nativer Discord-IP-Ban).")]
    public sealed class IpBanGroup
    {
        private readonly ModerationService _moderationService;

        public IpBanGroup(ModerationService moderationService)
        {
            _moderationService = moderationService;
        }

        [Command("add")]
        [Description("Fuegt eine IP zur internen Sperrliste hinzu.")]
        [RequirePermissions(DiscordPermission.ManageGuild)]
        public async ValueTask AddAsync(
            CommandContext ctx,
            [Description("IP-Adresse")] string ip,
            [Description("Optionale Notiz")] string? note = null)
        {
            if (ctx.Guild is null)
            {
                await ctx.RespondAsync("Dieser Befehl kann nur in einem Server genutzt werden.");
                return;
            }

            try
            {
                var result = await _moderationService.AddIpBanAsync(ctx.Guild.Id, ip, note, ctx.User.Id);
                if (!result.Added)
                {
                    await ctx.RespondAsync("Diese IP ist bereits in der internen Sperrliste vorhanden.");
                    return;
                }

                var embed = new DiscordEmbedBuilder()
                    .WithFeatureTitle("Moderation", "IP intern gesperrt", "🛡️")
                    .WithColor(SectorUI.SectorWarning)
                    .WithDescription(
                        $"Eintrag: **{result.Entry?.MaskedIp}**\n" +
                        $"Hinweis: Dies ist eine interne Bot-Liste, kein nativer Discord-IP-Ban.")
                    .WithTimestamp(DateTimeOffset.UtcNow);

                await ctx.RespondAsync(new DiscordMessageBuilder().AddEmbed(embed));
            }
            catch (Exception ex)
            {
                await ctx.RespondAsync($"IP konnte nicht gespeichert werden: {ex.Message}");
            }
        }

        [Command("remove")]
        [Description("Entfernt eine IP aus der internen Sperrliste.")]
        [RequirePermissions(DiscordPermission.ManageGuild)]
        public async ValueTask RemoveAsync(CommandContext ctx, [Description("IP-Adresse")] string ip)
        {
            if (ctx.Guild is null)
            {
                await ctx.RespondAsync("Dieser Befehl kann nur in einem Server genutzt werden.");
                return;
            }

            try
            {
                var removed = await _moderationService.RemoveIpBanAsync(ctx.Guild.Id, ip);
                await ctx.RespondAsync(removed
                    ? "IP wurde aus der internen Sperrliste entfernt."
                    : "IP war nicht in der internen Sperrliste vorhanden.");
            }
            catch (Exception ex)
            {
                await ctx.RespondAsync($"IP konnte nicht entfernt werden: {ex.Message}");
            }
        }

        [Command("list")]
        [Description("Zeigt die interne IP-Sperrliste an.")]
        [RequirePermissions(DiscordPermission.ManageGuild)]
        public async ValueTask ListAsync(CommandContext ctx)
        {
            if (ctx.Guild is null)
            {
                await ctx.RespondAsync("Dieser Befehl kann nur in einem Server genutzt werden.");
                return;
            }

            var entries = await _moderationService.GetIpBansAsync(ctx.Guild.Id);
            if (entries.Count == 0)
            {
                await ctx.RespondAsync("Die interne IP-Sperrliste ist leer.");
                return;
            }

            var lines = entries.Select(x =>
            {
                var note = string.IsNullOrWhiteSpace(x.Note) ? string.Empty : $" | Notiz: {x.Note}";
                return $"{x.MaskedIp} | durch <@{x.CreatedByUserId}> | {x.CreatedAtUtc:yyyy-MM-dd HH:mm} UTC{note}";
            });

            var embed = new DiscordEmbedBuilder()
                .WithFeatureTitle("Moderation", "Interne IP-Sperrliste", "🛡️")
                .WithColor(SectorUI.SectorInfoBlue)
                .WithDescription(string.Join("\n", lines))
                .WithFeatureFooter("Moderation", "Discord-Bots koennen keine echten IP-Bans im Discord-Netzwerk setzen.")
                .WithTimestamp(DateTimeOffset.UtcNow);

            await ctx.RespondAsync(new DiscordMessageBuilder().AddEmbed(embed));
        }
    }
}
