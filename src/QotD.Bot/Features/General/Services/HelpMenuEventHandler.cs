using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using System.Text;

namespace QotD.Bot.Features.General.Services;

public sealed class HelpMenuEventHandler : IEventHandler<ComponentInteractionCreatedEventArgs>
{
    public async Task HandleEventAsync(DiscordClient client, ComponentInteractionCreatedEventArgs e)
    {
        if (e.Interaction.Data.CustomId != "help_category_select") return;

        await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.DeferredMessageUpdate);

        var selectedCategory = e.Interaction.Data.Values[0];
        
        var embed = new DiscordEmbedBuilder()
            .WithColor(DiscordColor.Blurple)
            .WithTimestamp(DateTimeOffset.UtcNow);

        var sb = new StringBuilder();

        switch (selectedCategory)
        {
            case "general":
                embed.WithTitle("🛠️ General Commands");
                sb.AppendLine("> **`/help`** — Shows this interactive help dashboard.");
                sb.AppendLine("> **`/investigate <user>`** — Starts an analysis of the specified subject.");
                break;

            case "minigames":
                embed.WithTitle("🎮 MiniGames Commands");
                sb.AppendLine("> **`/blackjack`** — Starts a round of Blackjack.");
                sb.AppendLine("> **`/tower`** — Start reading the tower floors.");
                sb.AppendLine("> **`/counting setup`** — Sets up the counting channel.");
                sb.AppendLine("> **`/counting reset`** — Manual reset of the counter.");
                sb.AppendLine("> **`/wordchain setup`** — Sets up the wordchain channel.");
                sb.AppendLine("> **`/wordchain reset`** — Manual reset of the wordchain.");
                break;
                
            case "economy":
                embed.WithTitle("💰 Economy Commands");
                sb.AppendLine("> **Note:** Economy commands are primarily handled by external services or other specific commands in related bots.");
                break;
                
            case "qotd":
                embed.WithTitle("📅 Question of the Day");
                sb.AppendLine("> **`/qotd list`** — See upcoming scheduled questions.");
                sb.AppendLine("> **`/qotd add <date> <text>`** — Schedule a new question.");
                sb.AppendLine("> **`/qotd edit <id> [text] [date]`** — Modify an existing question.");
                sb.AppendLine("> **`/qotd delete <id>`** — Remove a scheduled question.");
                break;
                
            case "admin":
                embed.WithTitle("⚙️ Configuration & Admin");
                sb.AppendLine("> **`/qotd config channel <#channel>`** — Set the posting channel.");
                sb.AppendLine("> **`/qotd config time <HH:mm>`** — Set the daily posting time.");
                sb.AppendLine("> **`/qotd config role [role]`** — Set or clear the role to ping.");
                sb.AppendLine("> **`/qotd config template`** — Start an interactive template setup.");
                sb.AppendLine("> **`/qotd config show`** — Show current template and preview.");
                sb.AppendLine("> **`/qotd config reset`** — Reset template to default.");
                sb.AppendLine("> **`/qotd config test`** — Trigger a test post and thread.");
                sb.AppendLine("> **`/logsetup`** — Interactive logging configuration panel.");
                embed.AddField("Admin Access", "Most configuration commands require the **Manage Server** permission.", false);
                break;
        }

        embed.WithDescription(sb.ToString());
        embed.WithFooter("CozyCove System | Select a category below");

        await e.Interaction.EditOriginalResponseAsync(new DiscordWebhookBuilder()
            .AddEmbed(embed)
            .AddActionRowComponent(new DiscordActionRowComponent(new DiscordComponent[] { CreateSelectMenu(selectedCategory) })));
    }

    public static DiscordSelectComponent CreateSelectMenu(string placeholderCategoryCode = null)
    {
        var options = new List<DiscordSelectComponentOption>
        {
            new("General", "general", "Basic bot usage and info", false, new DiscordComponentEmoji(DiscordEmoji.FromUnicode("🛠️"))),
            new("MiniGames", "minigames", "Entertainment commands", false, new DiscordComponentEmoji(DiscordEmoji.FromUnicode("🎮"))),
            new("Economy", "economy", "Coins and balances", false, new DiscordComponentEmoji(DiscordEmoji.FromUnicode("💰"))),
            new("Question of the Day", "qotd", "QotD scheduling", false, new DiscordComponentEmoji(DiscordEmoji.FromUnicode("📅"))),
            new("Admin & Config", "admin", "Settings & Routing", false, new DiscordComponentEmoji(DiscordEmoji.FromUnicode("⚙️")))
        };

        var placeholder = "Select a Category...";
        if (placeholderCategoryCode != null)
        {
            var selected = options.FirstOrDefault(o => o.Value == placeholderCategoryCode);
            if (selected != null)
            {
                placeholder = "Viewing: " + selected.Label;
            }
        }

        return new DiscordSelectComponent("help_category_select", placeholder, options);
    }
}
