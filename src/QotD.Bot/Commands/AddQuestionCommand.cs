using DSharpPlus.Commands;
using DSharpPlus.Commands.Processors.SlashCommands;
using DSharpPlus.Commands.Processors.SlashCommands.ArgumentModifiers;
using DSharpPlus.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using QotD.Bot.Data;
using QotD.Bot.Data.Models;
using System.ComponentModel;
using QotD.Bot.UI;

namespace QotD.Bot.Commands;

/// <summary>
/// Slash command for administrators to schedule a new Question of the Day.
/// Usage: /add-question date:2026-03-07 text:What is your favourite IDE?
/// </summary>
[Command("add-question")]
[Description("Schedule a new Question of the Day for a specific date (Admin only).")]
public sealed class AddQuestionCommand
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AddQuestionCommand> _logger;

    public AddQuestionCommand(IServiceScopeFactory scopeFactory, ILogger<AddQuestionCommand> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    [Command("add-question")]
    public async ValueTask ExecuteAsync(
        CommandContext ctx,

        [Description("Date to post the question (YYYY-MM-DD).")]
        [SlashAutoCompleteProvider(typeof(DateAutoCompleteProvider))]
        string date,

        [Description("The question text to post (max 2000 characters).")]
        string text)
    {
        // Require ManageGuild permission (admin guard)
        if (ctx.Member is not null &&
            !ctx.Member.Permissions.HasPermission(DiscordPermission.ManageGuild))
        {
            await ctx.RespondAsync(
                new DiscordInteractionResponseBuilder()
                    .AddEmbed(CozyCoveUI.CreateErrorEmbed("You need the **Manage Server** permission to use this command."))
                    .AsEphemeral());
            return;
        }

        if (text.Length > 2000)
        {
            await ctx.RespondAsync(
                new DiscordInteractionResponseBuilder()
                    .AddEmbed(CozyCoveUI.CreateErrorEmbed("Question text must be 2000 characters or less."))
                    .AsEphemeral());
             return;
        }

        if (!DateOnly.TryParseExact(date, "yyyy-MM-dd", out var scheduledFor))
        {
            await ctx.RespondAsync(
                new DiscordInteractionResponseBuilder()
                    .AddEmbed(CozyCoveUI.CreateErrorEmbed("Invalid date format. Use `YYYY-MM-DD`, e.g. `2026-03-07`."))
                    .AsEphemeral());
            return;
        }

        if (scheduledFor < DateOnly.FromDateTime(DateTime.UtcNow))
        {
            await ctx.RespondAsync(
                new DiscordInteractionResponseBuilder()
                    .AddEmbed(CozyCoveUI.CreateErrorEmbed("Cannot schedule a question in the past."))
                    .AsEphemeral());
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var existing = await db.Questions
            .AsNoTracking()
            .FirstOrDefaultAsync(q => q.ScheduledFor == scheduledFor);

        if (existing is not null)
        {
            await ctx.RespondAsync(
                new DiscordInteractionResponseBuilder()
                    .AddEmbed(CozyCoveUI.CreateErrorEmbed($"A question is already scheduled for **{scheduledFor:yyyy-MM-dd}**:\n*\"{existing.QuestionText}\"*"))
                    .AsEphemeral());
            return;
        }

        var question = new Question
        {
            QuestionText = text,
            ScheduledFor = scheduledFor
        };

        db.Questions.Add(question);
        await db.SaveChangesAsync();

        _logger.LogInformation("Added question #{Id} for {Date}.", question.Id, scheduledFor);

        var embed = CozyCoveUI.CreateSuccessEmbed(
            $"**{text}**",
            "✅ Question Scheduled")
            .AddField("Date", scheduledFor.ToString("dddd, MMMM d, yyyy"), inline: true)
            .AddField("Question ID", $"#{question.Id}", inline: true)
            .WithTimestamp(DateTimeOffset.UtcNow);

        await ctx.RespondAsync(new DiscordInteractionResponseBuilder().AddEmbed(embed).AsEphemeral());
    }
}

/// <summary>
/// Optional: auto-completes the date field with the next 7 days.
/// </summary>
public sealed class DateAutoCompleteProvider : IAutoCompleteProvider
{
    public ValueTask<IEnumerable<DiscordAutoCompleteChoice>> AutoCompleteAsync(AutoCompleteContext ctx)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var suggestions = Enumerable.Range(1, 7)
            .Select(i => today.AddDays(i))
            .Select(d => new DiscordAutoCompleteChoice(d.ToString("yyyy-MM-dd"), d.ToString("yyyy-MM-dd")));

        return ValueTask.FromResult<IEnumerable<DiscordAutoCompleteChoice>>(suggestions);
    }
}
