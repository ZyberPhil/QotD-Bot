using DSharpPlus.Commands;
using DSharpPlus.Entities;
using QotD.Bot.UI;
using System.ComponentModel;
using System.Text;

namespace QotD.Bot.Commands;

/// <summary>
/// Command that provides information about the bot and its available commands.
/// Usage: /help
/// </summary>
public sealed class HelpCommand
{
    [Command("help")]
    [Description("Displays information about available commands and how to use the bot.")]
    public async ValueTask ExecuteAsync(CommandContext ctx)
    {
        var sb = new StringBuilder();

        sb.AppendLine("### 🛠️ General Commands");
        sb.AppendLine("> **`/help`** — Displays this message.");
        sb.AppendLine("> **`/investigate <user>`** — Starts an analysis of the specified subject.");
        sb.AppendLine();

        sb.AppendLine("### 📅 Question of the Day (QotD)");
        sb.AppendLine("> **`/qotd add <date> <text>`** — Schedule a new question.");
        sb.AppendLine("> **`/qotd list`** — See upcoming scheduled questions.");
        sb.AppendLine();

        sb.AppendLine("### ⚙️ Configuration");
        sb.AppendLine("> **`/qotd config channel <#channel>`** — Set the posting channel.");
        sb.AppendLine("> **`/qotd config time <HH:mm>`** — Set the daily posting time.");
        sb.AppendLine("> **`/qotd config template`** — Start an interactive template setup.");
        sb.AppendLine("> **`/qotd config show`** — Show current template and preview.");
        sb.AppendLine("> **`/qotd config reset`** — Reset template to default.");
        
        var embed = CozyCoveUI.CreateInfoEmbed(sb.ToString(), "📖 Botanical Manual — CozyCove")
            .AddField("Admin Access", "Most `/qotd` commands require the **Manage Server** permission.", false)
            .WithFooter("CozyCove System v1.0.0")
            .WithTimestamp(DateTimeOffset.UtcNow);

        await ctx.RespondAsync(new DiscordInteractionResponseBuilder()
            .AddEmbed(embed)
            .AsEphemeral());
    }
}
