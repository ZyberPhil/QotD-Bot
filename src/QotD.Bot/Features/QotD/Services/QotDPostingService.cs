using DSharpPlus;
using DSharpPlus.Entities;
using Microsoft.Extensions.Logging;
using QotD.Bot.Data.Models;
using QotD.Bot.UI;

namespace QotD.Bot.Features.QotD.Services;

/// <summary>
/// Encapsulates the full "post a QotD" workflow: build embed, send message,
/// send thread-hint, ghost-ping, and create a public thread.
/// Consumed by both <see cref="QotDBackgroundService"/> (scheduled posts) and
/// <see cref="QotDCommand"/> (manual test posts) so the logic lives in one place.
/// </summary>
public sealed class QotDPostingService(IServiceProvider serviceProvider, ILogger<QotDPostingService> logger)
{
    private DiscordClient discord => serviceProvider.GetRequiredService<DiscordClient>();

    public async Task PostQuestionAsync(
        DiscordChannel channel,
        string questionText,
        string questionId,
        GuildConfig config,
        DateOnly date,
        bool isTest = false)
    {
        DiscordEmbedBuilder embedBuilder;

        if (!string.IsNullOrWhiteSpace(config.MessageTemplate))
        {
            var description = BotPromptTokens.ApplyQotdTemplate(
                config.MessageTemplate,
                questionText,
                questionId,
                date.ToString("dd.MM.yyyy"));

            embedBuilder = CozyCoveUI.CreateBaseEmbed("❓ Frage des Tages", description);
        }
        else
        {
            var title = isTest ? "❓ Test: Frage des Tages" : "❓ Frage des Tages";
            embedBuilder = CozyCoveUI.CreateBaseEmbed(title, questionText);
        }

        embedBuilder.WithFooter($"{date:dddd, dd. MMMM yyyy}", CozyCoveUI.COZY_ICON_URL);

        var message = await channel.SendMessageAsync(
            new DiscordMessageBuilder().AddEmbed(embedBuilder.Build()));

        await channel.SendMessageAsync("> 🧵 *Die Antworten findet ihr im Thread unter dieser Nachricht!*");

        if (config.PingRoleId.HasValue)
        {
            var ghostPing = await channel.SendMessageAsync($"<@&{config.PingRoleId}>");
            await ghostPing.DeleteAsync();
        }

        await TryCreateThreadAsync(channel, message, date, isTest);
    }

    private async Task TryCreateThreadAsync(
        DiscordChannel channel, DiscordMessage? message, DateOnly date, bool isTest)
    {
        if (message == null) return;

        try
        {
            var currentMember = await channel.Guild.GetMemberAsync(discord.CurrentUser.Id);
            var permissions = channel.PermissionsFor(currentMember);

            if (!permissions.HasPermission(DiscordPermission.CreatePublicThreads))
            {
                logger.LogWarning(
                    "Missing 'CreatePublicThreads' permission in channel '{ChannelName}' ({ChannelId}) on Guild {GuildId}.",
                    channel.Name, channel.Id, channel.Guild.Id);
                return;
            }

            var threadName = isTest
                ? $"Test-Diskussion - {date:dd.MM.yyyy}"
                : $"QotD Answers - {date:dd.MM.yyyy}";

            await message.CreateThreadAsync(threadName, DiscordAutoArchiveDuration.Hour);

            logger.LogInformation(
                "Thread '{ThreadName}' created in channel {ChannelId}.", threadName, channel.Id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to create thread for message {MessageId} in channel {ChannelId}.",
                message?.Id, channel.Id);
        }
    }
}
