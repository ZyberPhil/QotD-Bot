using DSharpPlus.Commands;
using DSharpPlus.Entities;
using QotD.Bot.Features.General.Services;

namespace QotD.Bot.Features.General.Commands;

public sealed class HelpCommand
{
    [Command("help")]
    [System.ComponentModel.Description("Displays the interactive help dashboard.")]
    public async ValueTask ExecuteAsync(CommandContext ctx)
    {
        var embed = new DiscordEmbedBuilder()
            .WithTitle("📖 CozyCove Help Dashboard")
            .WithDescription("Welcome to the CozyCove Help System!\n\nPlease use the dropdown menu below to select a specific category and view its commands.")
            .WithColor(DiscordColor.Blurple)
            .WithFooter("CozyCove System v1.1.0")
            .WithTimestamp(DateTimeOffset.UtcNow);

        await ctx.RespondAsync(new DiscordInteractionResponseBuilder()
            .AddEmbed(embed)
            .AddActionRowComponent(new DiscordActionRowComponent(new DiscordComponent[] { HelpMenuEventHandler.CreateSelectMenu() }))
            .AsEphemeral());
    }
}
