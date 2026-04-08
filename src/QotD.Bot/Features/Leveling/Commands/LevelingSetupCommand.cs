using System.ComponentModel;
using DSharpPlus.Commands;
using DSharpPlus.Entities;
using QotD.Bot.Features.Leveling.Services;

namespace QotD.Bot.Features.Leveling.Commands;

[Command("levelingsetup")]
[Description("Levelings-Konfigurationsbefehle")]
public class LevelingSetupCommand
{
    private readonly LevelService _levelService;

    public LevelingSetupCommand(LevelService levelService)
    {
        _levelService = levelService;
    }

    [Command("setchannel")]
    [Description("Setzt den Kanal für Level-Up-Benachrichtigungen")]
    public async ValueTask SetChannelAsync(
        CommandContext ctx,
        [Description("Der Kanal für Level-Up-Meldungen")] DiscordChannel channel)
    {
        if (ctx.Guild is null)
        {
            await ctx.RespondAsync("Dieser Befehl kann nur in einem Server genutzt werden.");
            return;
        }

        // TODO: Check permissions (admin only)
        
        await _levelService.SetLevelUpChannelAsync(ctx.Guild.Id, channel.Id);

        var embed = new DiscordEmbedBuilder()
            .WithTitle("✅ Level-Up-Kanal konfiguriert")
            .WithColor(DiscordColor.Green)
            .WithDescription($"Level-Up-Meldungen werden jetzt in {channel.Mention} gepostet.")
            .WithTimestamp(DateTimeOffset.UtcNow);

        await ctx.RespondAsync(new DiscordMessageBuilder().AddEmbed(embed));
    }

    [Command("disablenotifications")]
    [Description("Deaktiviert Level-Up-Benachrichtigungen")]
    public async ValueTask DisableNotificationsAsync(CommandContext ctx)
    {
        if (ctx.Guild is null)
        {
            await ctx.RespondAsync("Dieser Befehl kann nur in einem Server genutzt werden.");
            return;
        }

        // TODO: Check permissions (admin only)

        await _levelService.DisableLevelUpNotificationsAsync(ctx.Guild.Id);

        var embed = new DiscordEmbedBuilder()
            .WithTitle("✅ Level-Up-Benachrichtigungen deaktiviert")
            .WithColor(DiscordColor.Green)
            .WithDescription("Level-Up-Meldungen werden nicht mehr gepostet.")
            .WithTimestamp(DateTimeOffset.UtcNow);

        await ctx.RespondAsync(new DiscordMessageBuilder().AddEmbed(embed));
    }
}
