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
            .WithColor(SectorUI.SectorPrimary)
            .WithTimestamp(DateTimeOffset.UtcNow);

        var sb = new StringBuilder();

        switch (selectedCategory)
        {
            case "general":
                embed.WithFeatureTitle("General", "Commands", "🛠️");
                sb.AppendLine("> **`/help`** - Shows this interactive help dashboard.");
                sb.AppendLine("> **`/investigate <user>`** - Starts an analysis of the specified subject.");
                break;

            case "minigames":
                embed.WithFeatureTitle("MiniGames", "Commands", "🎮");
                sb.AppendLine("> **`/blackjack [bet]`** - Starts a round of Blackjack.");
                sb.AppendLine("> **`/tower [bet]`** - Starts a Tower run.");
                sb.AppendLine("> **`/counting setup <#channel>`** - Sets up the counting channel.");
                sb.AppendLine("> **`/counting reset`** - Manual reset of the counter.");
                sb.AppendLine("> **`/wordchain setup <#channel>`** - Sets up the wordchain channel.");
                sb.AppendLine("> **`/wordchain reset`** - Manual reset of the wordchain.");
                break;

            case "leveling":
                embed.WithFeatureTitle("Leveling", "Commands", "📈");
                sb.AppendLine("> **`/rank [user]`** - Show level, XP and rank for a user.");
                sb.AppendLine("> **`/leaderboard`** - Show server leveling top list.");
                sb.AppendLine("> **`/levelingsetup setchannel <#channel>`** - Set level-up notification channel.");
                sb.AppendLine("> **`/levelingsetup disablenotifications`** - Disable level-up notifications.");
                sb.AppendLine("> **`/levelingsetup setbanner <url>`** - Set banner image for level-up embeds.");
                sb.AppendLine("> **`/levelingsetup clearbanner`** - Remove level-up embed banner.");
                sb.AppendLine("> **`/levelingsetup voiceconfig <minUsers> [allowMuted]`** - Configure voice XP rules.");
                break;

            case "teams":
                embed.WithFeatureTitle("Teams", "Commands", "👥");
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
                embed.WithFeatureTitle("Birthdays", "Commands", "🎂");
                sb.AppendLine("> **`/birthday set <day> <month>`** - Set your birthday reminder.");
                sb.AppendLine("> **`/birthday remove`** - Remove your birthday reminder.");
                sb.AppendLine("> **`/birthdaysetup <#channel> <@role>`** - Configure birthday announcements (Admin).");
                break;

            case "voice":
                embed.WithFeatureTitle("Temp Voice", "Commands", "🔊");
                sb.AppendLine("> **`/voice setup <trigger> [category]`** - Configure join-to-create temp voice (Admin).");
                sb.AppendLine("> **`/voice rename <name>`** - Rename your owned temp voice channel.");
                sb.AppendLine("> **`/voice limit <0-99>`** - Set user limit for your temp channel.");
                sb.AppendLine("> **`/voice lock`** - Lock your temp channel.");
                sb.AppendLine("> **`/voice unlock`** - Unlock your temp channel.");
                break;

            case "economy":
                embed.WithFeatureTitle("Economy", "Commands", "💰");
                sb.AppendLine("> Economy is used by game commands like `/blackjack` and `/tower`.");
                sb.AppendLine("> There are currently no direct economy slash commands in this bot.");
                break;

            case "selfroles":
                embed.WithFeatureTitle("Self Roles", "Commands", "🎭");
                sb.AppendLine("> **`/selfrolesetup status`** - Show current self-role panel configuration.");
                sb.AppendLine("> **`/selfrolesetup panel <#channel> [enabled] [...]`** - Configure panel behavior and appearance (Admin).");
                sb.AppendLine("> **`/selfrolesetup group <name> [exclusive] [order]`** - Create/update a role group (Admin).");
                sb.AppendLine("> **`/selfrolesetup optionadd <role> <emoji> <label> [...]`** - Add or update a self-role option (Admin).");
                sb.AppendLine("> **`/selfrolesetup optionremove <role>`** - Remove a self-role option (Admin).");
                sb.AppendLine("> **`/selfrolesetup publish`** - Render or refresh the panel message (Admin).");
                break;

            case "tickets":
                embed.WithFeatureTitle("Tickets", "Commands", "🎫");
                sb.AppendLine("> **`/ticket open [type] [priority] [subject]`** - Open a new ticket channel.");
                sb.AppendLine("> **`/ticket claim`** - Claim the current ticket as support staff.");
                sb.AppendLine("> **`/ticket close [reason]`** - Close current ticket and save transcript.");
                sb.AppendLine("> **`/ticket reopen`** - Reopen the current closed ticket.");
                sb.AppendLine("> **`/ticketsetup status`** - Show current ticket setup for this server (Admin).");
                sb.AppendLine("> **`/ticketsetup configure <category> <role> [maxOpen] [slaMinutes]`** - Configure ticket category and staff role (Admin).");
                sb.AppendLine("> **`/ticketsetup setlog <eventType> <#channel>`** - Route separate ticket log events (Admin).");
                sb.AppendLine("> Event types for `setlog`: `created`, `claimed`, `closed`, `reopened`, `escalated`.");
                break;

            case "linkmoderation":
                embed.WithFeatureTitle("Link Moderation", "Commands", "🔗");
                sb.AppendLine("> **`/linkfilter status`** - Show current link filter settings.");
                sb.AppendLine("> **`/linkfilter enable`** - Enable link filtering (Admin).");
                sb.AppendLine("> **`/linkfilter disable`** - Disable link filtering (Admin).");
                sb.AppendLine("> **`/linkfilter mode <whitelist|blacklist>`** - Set filtering mode (Admin).");
                sb.AppendLine("> **`/linkfilter logchannel [#channel]`** - Set or clear moderation log channel (Admin).");
                sb.AppendLine("> **`/linkfilter dmwarn <true|false>`** - Toggle DM warning for moderated links (Admin).");
                sb.AppendLine("> **`/linkfilter channelwarn <true|false>`** - Toggle in-channel warning (Admin).");
                sb.AppendLine("> **`/linkfilter ruleadd <domain>`** / **`ruleremove <domain>`** / **`rules`** - Manage domain rules.");
                sb.AppendLine("> **`/linkfilter bypassroleadd/remove/list`** - Manage bypass roles.");
                sb.AppendLine("> **`/linkfilter bypasschanneladd/remove/list`** - Manage bypass channels.");
                sb.AppendLine("");
                sb.AppendLine("> **`/automod status`** - Show raid mode and age-gate status.");
                sb.AppendLine("> **`/automod enable`** / **`disable`** - Toggle global automod (Admin).");
                sb.AppendLine("> **`/automod raidsettings <joins> <windowSec> <durationMin>`** - Configure raid trigger (Admin).");
                sb.AppendLine("> **`/automod verifiedrole [role]`** - Set or clear verified role for lockdown posting (Admin).");
                sb.AppendLine("> **`/automod lockdownrules <verifiedOnly> <minAccountAgeHours>`** - Strict lockdown posting rules (Admin).");
                sb.AppendLine("> **`/automod gates <enforceAccount> <days> <enforceServer> <hours>`** - Account/server age gates for links (Admin).");
                sb.AppendLine("> **`/automod audit [count]`** - Show recent automod audit entries (Admin).");
                break;

            case "qotd":
                embed.WithFeatureTitle("Question of the Day", "Commands", "📅");
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
                embed.WithFeatureTitle("Administration", "Configuration", "⚙️");
                sb.AppendLine("> **`/logsetup`** - Interactive logging configuration panel.");
                sb.AppendLine("> **`/ticketsetup status|configure|setlog`** - Ticket setup, limits and separate ticket log routing.");
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
        embed.WithFeatureFooter("Help", "Select a category below");

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
            new("Self Roles", "selfroles", "Self-assignable role panel", false, new DiscordComponentEmoji(DiscordEmoji.FromUnicode("🎭"))),
            new("Tickets", "tickets", "Support tickets and setup", false, new DiscordComponentEmoji(DiscordEmoji.FromUnicode("🎫"))),
            new("Link Moderation", "linkmoderation", "Automatic link filtering", false, new DiscordComponentEmoji(DiscordEmoji.FromUnicode("🔗"))),
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
