using DSharpPlus;
using DSharpPlus.Commands;
using DSharpPlus.Commands.Processors.SlashCommands;
using DSharpPlus.Commands.Processors.SlashCommands.ArgumentModifiers;
using DSharpPlus.Entities;
using DSharpPlus.Interactivity;
using DSharpPlus.Interactivity.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using QotD.Bot.Data;
using QotD.Bot.Data.Models;
using QotD.Bot.UI;
using System.ComponentModel;
using System.Text;

namespace QotD.Bot.Commands;

[Command("qotd")]
[Description("Manage Question of the Day features.")]
public sealed class QotDCommand
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<QotDCommand> _logger;

    public QotDCommand(IServiceScopeFactory scopeFactory, ILogger<QotDCommand> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    [Command("add")]
    [Description("Schedule a new Question of the Day for a specific date (Admin only).")]
    public async ValueTask AddAsync(
        CommandContext ctx,
        [Description("Date to post the question (YYYY-MM-DD).")] [SlashAutoCompleteProvider(typeof(DateAutoCompleteProvider))] string date,
        [Description("The question text to post (max 2000 characters).")] string text)
    {
        if (!await CheckPermissionsAsync(ctx)) return;

        if (text.Length > 2000)
        {
            await ctx.RespondAsync(new DiscordInteractionResponseBuilder()
                .AddEmbed(CozyCoveUI.CreateErrorEmbed("Question text must be 2000 characters or less."))
                .AsEphemeral());
            return;
        }

        if (!DateOnly.TryParseExact(date, "yyyy-MM-dd", out var scheduledFor))
        {
            await ctx.RespondAsync(new DiscordInteractionResponseBuilder()
                .AddEmbed(CozyCoveUI.CreateErrorEmbed("Invalid date format. Use `YYYY-MM-DD`, e.g. `2026-03-07`."))
                .AsEphemeral());
            return;
        }

        if (scheduledFor < DateOnly.FromDateTime(DateTime.UtcNow))
        {
            await ctx.RespondAsync(new DiscordInteractionResponseBuilder()
                .AddEmbed(CozyCoveUI.CreateErrorEmbed("Cannot schedule a question in the past."))
                .AsEphemeral());
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var existing = await db.Questions.AsNoTracking().FirstOrDefaultAsync(q => q.ScheduledFor == scheduledFor);
        if (existing is not null)
        {
            await ctx.RespondAsync(new DiscordInteractionResponseBuilder()
                .AddEmbed(CozyCoveUI.CreateErrorEmbed($"A question is already scheduled for **{scheduledFor:yyyy-MM-dd}**:\n*\"{existing.QuestionText}\"*"))
                .AsEphemeral());
            return;
        }

        var question = new Question { QuestionText = text, ScheduledFor = scheduledFor };
        db.Questions.Add(question);
        await db.SaveChangesAsync();

        _logger.LogInformation("Added question #{Id} for {Date}.", question.Id, scheduledFor);

        var embed = CozyCoveUI.CreateSuccessEmbed($"**{text}**", "✅ Question Scheduled")
            .AddField("Date", scheduledFor.ToString("dddd, MMMM d, yyyy"), inline: true)
            .AddField("Question ID", $"#{question.Id}", inline: true)
            .WithTimestamp(DateTimeOffset.UtcNow);

        await ctx.RespondAsync(new DiscordInteractionResponseBuilder().AddEmbed(embed).AsEphemeral());
    }

    [Command("list")]
    [Description("List all upcoming unposted Questions of the Day (Admin only).")]
    public async ValueTask ListAsync(CommandContext ctx)
    {
        if (!await CheckPermissionsAsync(ctx)) return;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var questions = await db.Questions
            .AsNoTracking()
            .Where(q => !q.Posted && q.ScheduledFor >= today)
            .OrderBy(q => q.ScheduledFor)
            .Take(25)
            .ToListAsync();

        if (questions.Count == 0)
        {
            await ctx.RespondAsync(new DiscordInteractionResponseBuilder()
                .AddEmbed(CozyCoveUI.CreateInfoEmbed("No upcoming questions are scheduled.", "📭 Queue Empty"))
                .AsEphemeral());
            return;
        }

        var sb = new StringBuilder();
        foreach (var q in questions)
        {
            sb.AppendLine($"> **{q.ScheduledFor:yyyy-MM-dd}** (`#{q.Id}`) — {q.QuestionText[..Math.Min(80, q.QuestionText.Length)]}{(q.QuestionText.Length > 80 ? "…" : "")}");
        }

        var embed = CozyCoveUI.CreateBaseEmbed($"📅 Upcoming Questions ({questions.Count})", sb.ToString())
            .WithFooter("Showing next 25 unposted questions")
            .WithTimestamp(DateTimeOffset.UtcNow);

        await ctx.RespondAsync(new DiscordInteractionResponseBuilder().AddEmbed(embed).AsEphemeral());
    }

    [Command("config")]
    [Description("Configure QotD settings (Admin only).")]
    public sealed class ConfigGroup
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<QotDCommand> _logger;

        public ConfigGroup(IServiceScopeFactory scopeFactory, ILogger<QotDCommand> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        [Command("channel")]
        [Description("Set the channel where questions will be posted.")]
        public async ValueTask SetChannelAsync(CommandContext ctx, [Description("The target channel")] DiscordChannel channel)
        {
            if (!await CheckPermissionsAsync(ctx)) return;

            if (channel.Type != DiscordChannelType.Text)
            {
                await ctx.RespondAsync(new DiscordInteractionResponseBuilder().AddEmbed(CozyCoveUI.CreateErrorEmbed("Please select a text channel.")).AsEphemeral());
                return;
            }

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var config = await GetOrCreateConfigAsync(db, ctx.Guild!.Id);
            config.ChannelId = channel.Id;
            await db.SaveChangesAsync();

            await ctx.RespondAsync(new DiscordInteractionResponseBuilder()
                .AddEmbed(CozyCoveUI.CreateSuccessEmbed($"Question of the Day will now be posted in {channel.Mention}.", "✅ Channel Updated"))
                .AsEphemeral());
        }

        [Command("time")]
        [Description("Set the time when the question will be posted (HH:mm).")]
        public async ValueTask SetTimeAsync(CommandContext ctx, [Description("Time in HH:mm format (e.g., 08:30)")] string time)
        {
            if (!await CheckPermissionsAsync(ctx)) return;

            if (!TimeOnly.TryParseExact(time, "HH:mm", out var postTime))
            {
                await ctx.RespondAsync(new DiscordInteractionResponseBuilder().AddEmbed(CozyCoveUI.CreateErrorEmbed("Invalid time format. Please use HH:mm.")).AsEphemeral());
                return;
            }

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var config = await GetOrCreateConfigAsync(db, ctx.Guild!.Id);
            config.PostTime = postTime;
            await db.SaveChangesAsync();

            await ctx.RespondAsync(new DiscordInteractionResponseBuilder()
                .AddEmbed(CozyCoveUI.CreateSuccessEmbed($"Question of the Day will now be posted at **{postTime:HH:mm}**.", "✅ Time Updated"))
                .AsEphemeral());
        }

        [Command("template")]
        [Description("Set a custom message template.")]
        public async ValueTask SetTemplateAsync(CommandContext ctx)
        {
            if (!await CheckPermissionsAsync(ctx)) return;

            await ctx.RespondAsync(new DiscordInteractionResponseBuilder()
                .AddEmbed(CozyCoveUI.CreateInfoEmbed("📝 Bitte sende jetzt die Nachricht für das Template.\nPlatzhalter: `{message}`, `{date}`, `{id}`.", "Template Setup"))
                .AsEphemeral());

            try
            {
                var interactivity = ctx.ServiceProvider.GetRequiredService<InteractivityExtension>();
                var result = await interactivity.WaitForMessageAsync(x => x.Author?.Id == ctx.User.Id && x.ChannelId == ctx.Channel.Id, TimeSpan.FromMinutes(5));

                if (result.TimedOut)
                {
                    await ctx.FollowupAsync(new DiscordFollowupMessageBuilder().AddEmbed(CozyCoveUI.CreateErrorEmbed("Zeitüberschreitung.")).AsEphemeral());
                    return;
                }

                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var config = await GetOrCreateConfigAsync(db, ctx.Guild!.Id);
                config.MessageTemplate = result.Result.Content;
                await db.SaveChangesAsync();

                await ctx.FollowupAsync(new DiscordFollowupMessageBuilder().AddEmbed(CozyCoveUI.CreateSuccessEmbed("Template gespeichert!", "✅ Template Saved")).AsEphemeral());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to capture template.");
                await ctx.FollowupAsync(new DiscordFollowupMessageBuilder().AddEmbed(CozyCoveUI.CreateErrorEmbed("Fehler beim Speichern.")).AsEphemeral());
            }
        }

        [Command("show")]
        [Description("Show the current template and preview.")]
        public async ValueTask ShowTemplateAsync(CommandContext ctx)
        {
            if (!await CheckPermissionsAsync(ctx)) return;

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var config = await GetOrCreateConfigAsync(db, ctx.Guild!.Id);

            if (string.IsNullOrWhiteSpace(config.MessageTemplate))
            {
                await ctx.RespondAsync(new DiscordInteractionResponseBuilder().AddEmbed(CozyCoveUI.CreateInfoEmbed("Kein Template gesetzt.", "ℹ️ Info")).AsEphemeral());
                return;
            }

            var preview = config.MessageTemplate.Replace("{message}", "Vorschau Text").Replace("{date}", DateTime.Now.ToString("d")).Replace("{id}", "1");
            await ctx.RespondAsync(new DiscordInteractionResponseBuilder().AddEmbed(CozyCoveUI.CreateBaseEmbed("Template Vorschau", preview)).AsEphemeral());
        }

        [Command("reset")]
        [Description("Reset template to default.")]
        public async ValueTask ResetTemplateAsync(CommandContext ctx)
        {
            if (!await CheckPermissionsAsync(ctx)) return;

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var config = await GetOrCreateConfigAsync(db, ctx.Guild!.Id);
            config.MessageTemplate = null;
            await db.SaveChangesAsync();

            await ctx.RespondAsync(new DiscordInteractionResponseBuilder().AddEmbed(CozyCoveUI.CreateSuccessEmbed("Template zurückgesetzt.", "✅ Reset")).AsEphemeral());
        }

        [Command("test")]
        [Description("Triggers a test post using the current template and creates a thread.")]
        public async ValueTask TestPostAsync(CommandContext ctx)
        {
            if (!await CheckPermissionsAsync(ctx)) return;

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var config = await GetOrCreateConfigAsync(db, ctx.Guild!.Id);

            if (config.ChannelId == 0)
            {
                await ctx.RespondAsync(new DiscordInteractionResponseBuilder()
                    .AddEmbed(CozyCoveUI.CreateErrorEmbed("Es wurde kein Kanal für QotD konfiguriert. Nutze `/qotd config channel`."))
                    .AsEphemeral());
                return;
            }

            var channel = await ctx.Client.GetChannelAsync(config.ChannelId);

            // Robust Permission Check for the bot in the target channel
            var botMember = await channel.Guild.GetMemberAsync(ctx.Client.CurrentUser.Id);
            var botPermissions = channel.PermissionsFor(botMember);

            if (!botPermissions.HasPermission(DiscordPermission.SendMessages))
            {
                await ctx.RespondAsync(new DiscordInteractionResponseBuilder()
                    .AddEmbed(CozyCoveUI.CreateErrorEmbed($"Ich habe keine Berechtigung, Nachrichten in <#{config.ChannelId}> zu senden. Bitte prüfe meine Rollen und Kanal-Berechtigungen."))
                    .AsEphemeral());
                return;
            }

            var dateOnly = DateOnly.FromDateTime(DateTime.Now);
            var testQuestion = "Dies ist eine Test-Frage, um das Template und die Thread-Erstellung zu prüfen.";
            
            DiscordMessage message;

            try
            {
                var embedBuilder = new DiscordEmbedBuilder()
                    .WithColor(new DiscordColor("#7289DA"))
                    .WithTimestamp(DateTimeOffset.UtcNow);

                if (!string.IsNullOrWhiteSpace(config.MessageTemplate))
                {
                    var formattedDescription = config.MessageTemplate
                        .Replace("{message}", testQuestion)
                        .Replace("{date}", dateOnly.ToString("dd.MM.yyyy"))
                        .Replace("{id}", "999");

                    embedBuilder
                        .WithTitle("❓ Frage des Tages")
                        .WithDescription(formattedDescription)
                        .WithFooter($"Testbeitrag #999 · {dateOnly:dddd, dd. MMMM yyyy}");
                }
                else
                {
                    embedBuilder
                        .WithTitle("❓ Test: Frage des Tages")
                        .WithDescription($"{testQuestion}\n\n*Gerne kannst du deine Gedanken im Thread unten teilen!*")
                        .WithFooter($"Testbeitrag #999 · {dateOnly:dddd, dd. MMMM yyyy}");
                }

                message = await channel.SendMessageAsync(new DiscordMessageBuilder()
                    .WithContent("> 🧵 **Die Diskussion findet im Thread unter dieser Nachricht statt!**")
                    .AddEmbed(embedBuilder.Build()));
            }
            catch (DSharpPlus.Exceptions.DiscordException ex)
            {
                _logger.LogError(ex, "Failed to send test message to channel {ChannelId}.", config.ChannelId);
                await ctx.RespondAsync(new DiscordInteractionResponseBuilder()
                    .AddEmbed(CozyCoveUI.CreateErrorEmbed($"Discord API Fehler: {ex.Message} (Stelle sicher, dass ich den Kanal sehen und darin schreiben darf)."))
                    .AsEphemeral());
                return;
            }

            // Manuelle Thread-Erstellung (analog zum QotDBackgroundService)
            var currentMember = await channel.Guild.GetMemberAsync(ctx.Client.CurrentUser.Id);
            var permissions = channel.PermissionsFor(currentMember);

            if (permissions.HasPermission(DiscordPermission.CreatePublicThreads))
            {
                var threadName = $"Test-Diskussion - {dateOnly:dd.MM.yyyy}";
                await message.CreateThreadAsync(threadName, DiscordAutoArchiveDuration.Hour);
            }

            await ctx.RespondAsync(new DiscordInteractionResponseBuilder()
                .AddEmbed(CozyCoveUI.CreateSuccessEmbed($"Test-Nachricht wurde in <#{config.ChannelId}> gesendet.", "✅ Test erfolgreich"))
                .AsEphemeral());
        }
    }

    private static async Task<bool> CheckPermissionsAsync(CommandContext ctx)
    {
        if (ctx.Guild is null)
        {
            await ctx.RespondAsync(new DiscordInteractionResponseBuilder().AddEmbed(CozyCoveUI.CreateErrorEmbed("Nur im Server nutzbar.")).AsEphemeral());
            return false;
        }

        if (ctx.Member is null || !ctx.Member.Permissions.HasPermission(DiscordPermission.ManageGuild))
        {
            await ctx.RespondAsync(new DiscordInteractionResponseBuilder().AddEmbed(CozyCoveUI.CreateErrorEmbed("Berechtigung **Server verwalten** erforderlich.")).AsEphemeral());
            return false;
        }
        return true;
    }

    private static async Task<GuildConfig> GetOrCreateConfigAsync(AppDbContext db, ulong guildId)
    {
        var config = await db.GuildConfigs.FirstOrDefaultAsync(g => g.GuildId == guildId);
        if (config == null)
        {
            config = new GuildConfig { GuildId = guildId };
            db.GuildConfigs.Add(config);
        }
        return config;
    }
}

public sealed class DateAutoCompleteProvider : IAutoCompleteProvider
{
    public ValueTask<IEnumerable<DiscordAutoCompleteChoice>> AutoCompleteAsync(AutoCompleteContext ctx)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var suggestions = Enumerable.Range(1, 7).Select(i => today.AddDays(i)).Select(d => new DiscordAutoCompleteChoice(d.ToString("yyyy-MM-dd"), d.ToString("yyyy-MM-dd")));
        return ValueTask.FromResult<IEnumerable<DiscordAutoCompleteChoice>>(suggestions);
    }
}
