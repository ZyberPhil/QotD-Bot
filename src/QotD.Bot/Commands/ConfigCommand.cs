using DSharpPlus;
using DSharpPlus.Commands;
using DSharpPlus.Commands.Processors.SlashCommands;
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

namespace QotD.Bot.Commands;

[Command("qotd")]
[Description("Manage Question of the Day settings.")]
public sealed class ConfigCommand
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ConfigCommand> _logger;

    public ConfigCommand(IServiceScopeFactory scopeFactory, ILogger<ConfigCommand> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    [Command("config")]
    [Description("Configure QotD settings (Admin only).")]
    public sealed class ConfigGroup
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<ConfigCommand> _logger;

        public ConfigGroup(IServiceScopeFactory scopeFactory, ILogger<ConfigCommand> logger)
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
                await ctx.RespondAsync(new DiscordInteractionResponseBuilder()
                    .AddEmbed(CozyCoveUI.CreateErrorEmbed("Please select a text channel."))
                    .AsEphemeral());
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
                await ctx.RespondAsync(new DiscordInteractionResponseBuilder()
                    .AddEmbed(CozyCoveUI.CreateErrorEmbed("Invalid time format. Please use HH:mm (e.g., 07:00, 22:30)."))
                    .AsEphemeral());
                return;
            }

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var config = await GetOrCreateConfigAsync(db, ctx.Guild!.Id);
            config.PostTime = postTime;

            await db.SaveChangesAsync();
            await ctx.RespondAsync(new DiscordInteractionResponseBuilder()
                .AddEmbed(CozyCoveUI.CreateSuccessEmbed($"Question of the Day will now be posted at **{postTime:HH:mm}** ({config.Timezone}).", "✅ Time Updated"))
                .AsEphemeral());
        }

        [Command("template")]
        [Description("Set a custom message template. The bot will wait for your next message.")]
        public async ValueTask SetTemplateAsync(CommandContext ctx)
        {
            if (!await CheckPermissionsAsync(ctx)) return;

            _logger.LogInformation("Starting template capture for user {User} in channel {Channel}", ctx.User.Id, ctx.Channel.Id);

            await ctx.RespondAsync(new DiscordInteractionResponseBuilder()
                .AddEmbed(CozyCoveUI.CreateInfoEmbed(
                    "📝 Bitte sende jetzt die Nachricht, die als Template dienen soll. Du kannst Zeilenumbrüche und Designs verwenden.\nVerfügbare Platzhalter: `{message}`, `{date}`, `{id}`.\n*(Du hast 5 Minuten Zeit)*",
                    "Template Configuration"))
                .AsEphemeral());

            try
            {
                var interactivity = ctx.ServiceProvider.GetRequiredService<InteractivityExtension>();
                _logger.LogInformation("Interactivity extension resolved. Waiting for message...");

                var result = await interactivity.WaitForMessageAsync(
                    x => {
                        var match = x.Author?.Id == ctx.User.Id && x.ChannelId == ctx.Channel.Id;
                        if (match) _logger.LogInformation("Message match detected: {Content}", x.Content);
                        return match;
                    },
                    TimeSpan.FromMinutes(5));

                if (result.TimedOut)
                {
                    _logger.LogWarning("Template capture timed out for user {User}", ctx.User.Id);
                    await ctx.FollowupAsync(new DiscordFollowupMessageBuilder()
                        .AddEmbed(CozyCoveUI.CreateErrorEmbed("Zeitüberschreitung. Die Template-Konfiguration wurde abgebrochen. Hast du den 'Message Content Intent' im Discord Developer Portal wirklich aktiviert?"))
                        .AsEphemeral());
                    return;
                }

                _logger.LogInformation("Template captured successfully: {Length} characters", result.Result.Content.Length);

                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var config = await GetOrCreateConfigAsync(db, ctx.Guild!.Id);
                config.MessageTemplate = result.Result.Content;

                await db.SaveChangesAsync();
                await ctx.FollowupAsync(new DiscordFollowupMessageBuilder()
                    .AddEmbed(CozyCoveUI.CreateSuccessEmbed("Das neue Template wurde gespeichert! Du kannst es mit `/qotd config show` ansehen.", "✅ Template Saved"))
                    .AsEphemeral());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save template via interactivity.");
                await ctx.FollowupAsync(new DiscordFollowupMessageBuilder()
                    .AddEmbed(CozyCoveUI.CreateErrorEmbed("Ein interner Fehler ist aufgetreten."))
                    .AsEphemeral());
            }
        }

        [Command("reset")]
        [Description("Reset the message template to the default embed.")]
        public async ValueTask ResetTemplateAsync(CommandContext ctx)
        {
            if (!await CheckPermissionsAsync(ctx)) return;

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var config = await GetOrCreateConfigAsync(db, ctx.Guild!.Id);
            config.MessageTemplate = null;

            await db.SaveChangesAsync();
            await ctx.RespondAsync(new DiscordInteractionResponseBuilder()
                .AddEmbed(CozyCoveUI.CreateSuccessEmbed("Message template has been reset to the default embed.", "✅ Template Reset"))
                .AsEphemeral());
        }

        [Command("show")]
        [Description("Show the current message template and a preview.")]
        public async ValueTask ShowTemplateAsync(CommandContext ctx)
        {
            if (!await CheckPermissionsAsync(ctx)) return;

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var config = await GetOrCreateConfigAsync(db, ctx.Guild!.Id);

            if (string.IsNullOrWhiteSpace(config.MessageTemplate))
            {
                await ctx.RespondAsync(new DiscordInteractionResponseBuilder()
                    .AddEmbed(CozyCoveUI.CreateInfoEmbed("No custom template set. Using default embed.", "ℹ️ Template Info"))
                    .AsEphemeral());
                return;
            }

            var preview = config.MessageTemplate
                .Replace("{message}", "This is a sample question text.")
                .Replace("{date}", DateTime.Now.ToString("dd.MM.yyyy"))
                .Replace("{id}", "123");

            var response = $"**Current Template:**\n```\n{config.MessageTemplate}\n```\n**Preview:**\n{preview}";
            
            await ctx.RespondAsync(new DiscordInteractionResponseBuilder()
                .AddEmbed(CozyCoveUI.CreateBaseEmbed("Template Preview", response))
                .AsEphemeral());
        }
    }

    private static async Task<bool> CheckPermissionsAsync(CommandContext ctx)
    {
        if (ctx.Guild is null)
        {
            await ctx.RespondAsync(new DiscordInteractionResponseBuilder()
                .AddEmbed(CozyCoveUI.CreateErrorEmbed("This command can only be used in a server."))
                .AsEphemeral());
            return false;
        }

        if (ctx.Member is null || !ctx.Member.Permissions.HasPermission(DiscordPermission.ManageGuild))
        {
            await ctx.RespondAsync(new DiscordInteractionResponseBuilder()
                .AddEmbed(CozyCoveUI.CreateErrorEmbed("You need the **Manage Server** permission to use this command."))
                .AsEphemeral());
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
