using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using QotD.Bot.UI;
using System.Text;

namespace QotD.Bot.Features.General.Services;

public sealed class HelpMenuEventHandler : IEventHandler<ComponentInteractionCreatedEventArgs>
{
    public async Task HandleEventAsync(DiscordClient client, ComponentInteractionCreatedEventArgs e)
    {
        if (e.Interaction.Data.CustomId != "help_category_select")
        {
            return;
        }

        await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.DeferredMessageUpdate);

        var selectedCategory = e.Interaction.Data.Values[0];

        var embed = new DiscordEmbedBuilder()
            .WithColor(CozyCoveUI.CozyPrimary)
            .WithTimestamp(DateTimeOffset.UtcNow);

        var sb = new StringBuilder();

        switch (selectedCategory)
        {
            case "general":
                embed.WithTitle("🛠️ General Commands");
                sb.AppendLine("> **`/help`** - Shows this interactive help dashboard.");
                sb.AppendLine("> **`/investigate <user>`** - Starts an analysis of the specified subject.");
                break;

            case "minigames":
                embed.WithTitle("🎮 MiniGames Commands");
                sb.AppendLine("> **`/blackjack [bet]`** - Starts a round of Blackjack.");
                sb.AppendLine("> **`/tower [bet]`** - Starts a Tower run.");
                sb.AppendLine("> **`/counting setup <#channel>`** - Sets up the counting channel.");
                sb.AppendLine("> **`/counting reset`** - Manual reset of the counter.");
                sb.AppendLine("> **`/wordchain setup <#channel>`** - Sets up the wordchain channel.");
                sb.AppendLine("> **`/wordchain reset`** - Manual reset of the wordchain.");
                break;

            case "leveling":
                embed.WithTitle("📈 Leveling Commands");
                sb.AppendLine("> **`/rank [user]`** - Show level, XP and rank for a user.");
                sb.AppendLine("> **`/leaderboard`** - Show server leveling top list.");
                sb.AppendLine("> **`/levelingsetup setchannel <#channel>`** - Set level-up notification channel.");
                sb.AppendLine("> **`/levelingsetup disablenotifications`** - Disable level-up notifications.");
                sb.AppendLine("> **`/levelingsetup voiceconfig <minUsers> [allowMuted]`** - Configure voice XP rules.");
                break;

            case "teams":
                embed.WithTitle("👥 Team Commands");
                sb.AppendLine("> **`/teamsetup`** - Interactive team role tracking setup panel.");
                sb.AppendLine("> **`/team me`** - Shows your current weekly progress and warning status.");
                sb.AppendLine("> **`/team ranking`** - Weekly team activity ranking.");
                sb.AppendLine("> **`/team minima <role> <messages> <voiceMinutes>`** - Set weekly activity minimum per role (Admin).");
                sb.AppendLine("> **`/team reportsetup <#channel>`** - Set the weekly report channel (Admin)." );
                sb.AppendLine("> **`/team reportdisable`** - Disable the weekly report (Admin)." );
                sb.AppendLine("> **`/team warnings [user]`** - Show active team warnings (Admin).");
                sb.AppendLine("> **`/team warningsadd <user> <reason>`** - Add a manual warning (Admin).");
                sb.AppendLine("> **`/team warningsremove <id>`** - Remove/deactivate warning by ID (Admin).");
                sb.AppendLine("> **`/team warningsnote lead <id> <text>`** - Add a teamlead comment to a warning (Admin)." );
                sb.AppendLine("> **`/team warningsnote statement <id> <text>`** - Add your statement to a warning." );
                sb.AppendLine("> **`/team warningsnote resolve <id> <text>`** - Close a warning with a resolution note (Admin)." );
                sb.AppendLine("> **`/team warningsnote list <id>`** - Show all notes for a warning." );
                sb.AppendLine("> **`/team rolehistory [user]`** - Show tracked role changes for a team member." );
                sb.AppendLine("> **`/team leavestart <reason> [days]`** - Start leave/absence with reason.");
                sb.AppendLine("> **`/team leaveend`** - End active leave.");
                sb.AppendLine("> **`/team leavestats [user]`** - Show leave count and total duration.");
                sb.AppendLine("> **`/team leavehistory [user]`** - Show leave history with reason and period.");
                break;

            case "birthdays":
                embed.WithTitle("🎂 Birthday Commands");
                sb.AppendLine("> **`/birthday set <day> <month>`** - Set your birthday reminder.");
                sb.AppendLine("> **`/birthday remove`** - Remove your birthday reminder.");
                sb.AppendLine("> **`/birthdaysetup <#channel> <@role>`** - Configure birthday announcements (Admin).");
                break;

            case "voice":
                embed.WithTitle("🔊 Temp Voice Commands");
                sb.AppendLine("> **`/voice setup <trigger> [category]`** - Configure join-to-create temp voice (Admin).");
                sb.AppendLine("> **`/voice rename <name>`** - Rename your owned temp voice channel.");
                sb.AppendLine("> **`/voice limit <0-99>`** - Set user limit for your temp channel.");
                sb.AppendLine("> **`/voice lock`** - Lock your temp channel.");
                sb.AppendLine("> **`/voice unlock`** - Unlock your temp channel.");
                break;

            case "economy":
                embed.WithTitle("💰 Economy Commands");
                sb.AppendLine("> Economy is used by game commands like `/blackjack` and `/tower`.");
                sb.AppendLine("> There are currently no direct economy slash commands in this bot.");
                break;

            case "qotd":
                embed.WithTitle("📅 Question of the Day");
                sb.AppendLine("> **`/qotd list`** - See upcoming scheduled questions.");
                sb.AppendLine("> **`/qotd add <date> <text>`** - Schedule a new question.");
                sb.AppendLine("> **`/qotd edit <id> [text] [date]`** - Modify an existing question.");
                sb.AppendLine("> **`/qotd delete <id>`** - Remove a scheduled question.");
                sb.AppendLine("> **`/qotd config channel <#channel>`** - Set posting channel.");
                sb.AppendLine("> **`/qotd config time <HH:mm>`** - Set daily posting time.");
                sb.AppendLine("> **`/qotd config role [role]`** - Set or clear ping role.");
                sb.AppendLine("> **`/qotd config template`** - Start interactive template setup.");
                sb.AppendLine("> **`/qotd config show`** - Show current template and preview.");
                sb.AppendLine("> **`/qotd config reset`** - Reset template to default.");
                sb.AppendLine("> **`/qotd config test`** - Trigger test post and thread.");
                break;

            case "admin":
                embed.WithTitle("⚙️ Configuration and Admin");
                sb.AppendLine("> **`/logsetup`** - Interactive logging configuration panel.");
                sb.AppendLine("> **`/teamsetup`** - Team roles setup panel.");
                sb.AppendLine("> **`/team me`** - Your weekly team status.");
                sb.AppendLine("> **`/team reportsetup`** - Weekly report channel.");
                sb.AppendLine("> **`/birthdaysetup`** - Birthday module setup.");
                sb.AppendLine("> **`/voice setup`** - Temp voice setup.");
                sb.AppendLine("> **`/levelingsetup ...`** - Leveling setup commands.");
                sb.AppendLine("> **`/qotd config ...`** - QotD configuration commands.");
                embed.AddField("Admin Access", "Most configuration commands require the **Manage Server** permission.", false);
                break;
        }

        embed.WithDescription(sb.ToString());
        embed.WithFooter("CozyCove System | Select a category below");

        await e.Interaction.EditOriginalResponseAsync(new DiscordWebhookBuilder()
            .AddEmbed(embed)
            .AddActionRowComponent(new DiscordActionRowComponent(new DiscordComponent[] { CreateSelectMenu(selectedCategory) })));
    }

    public static DiscordSelectComponent CreateSelectMenu(string? placeholderCategoryCode = null)
    {
        var options = new List<DiscordSelectComponentOption>
        {
            new("General", "general", "Basic bot usage and info", false, new DiscordComponentEmoji(DiscordEmoji.FromUnicode("🛠️"))),
            new("MiniGames", "minigames", "Entertainment commands", false, new DiscordComponentEmoji(DiscordEmoji.FromUnicode("🎮"))),
            new("Leveling", "leveling", "XP, rank and leaderboard", false, new DiscordComponentEmoji(DiscordEmoji.FromUnicode("📈"))),
            new("Teams", "teams", "Team activity, warnings and leave", false, new DiscordComponentEmoji(DiscordEmoji.FromUnicode("👥"))),
            new("Birthdays", "birthdays", "Birthday reminders and setup", false, new DiscordComponentEmoji(DiscordEmoji.FromUnicode("🎂"))),
            new("Temp Voice", "voice", "Temporary voice channel controls", false, new DiscordComponentEmoji(DiscordEmoji.FromUnicode("🔊"))),
            new("Economy", "economy", "Coins and balances", false, new DiscordComponentEmoji(DiscordEmoji.FromUnicode("💰"))),
            new("Question of the Day", "qotd", "QotD scheduling", false, new DiscordComponentEmoji(DiscordEmoji.FromUnicode("📅"))),
            new("Admin and Config", "admin", "Settings and routing", false, new DiscordComponentEmoji(DiscordEmoji.FromUnicode("⚙️")))
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
