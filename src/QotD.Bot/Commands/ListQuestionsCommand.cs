using DSharpPlus.Commands;
using DSharpPlus.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QotD.Bot.Data;
using System.ComponentModel;
using System.Text;

namespace QotD.Bot.Commands;

/// <summary>
/// Slash command that lists all upcoming (unposted) questions (Admin only).
/// Usage: /list-questions
/// </summary>
[Command("list-questions")]
[Description("List all upcoming unposted Questions of the Day (Admin only).")]
public sealed class ListQuestionsCommand
{
    private readonly IServiceScopeFactory _scopeFactory;

    public ListQuestionsCommand(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    [Command("list-questions")]
    public async ValueTask ExecuteAsync(CommandContext ctx)
    {
        // Require ManageGuild permission (admin guard)
        if (ctx.Member is not null &&
            !ctx.Member.Permissions.HasPermission(DSharpPlus.Entities.DiscordPermission.ManageGuild))
        {
            await ctx.RespondAsync(
                new DiscordInteractionResponseBuilder()
                    .WithContent("❌ You need the **Manage Server** permission to use this command.")
                    .AsEphemeral());
            return;
        }

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
            await ctx.RespondAsync(
                new DiscordInteractionResponseBuilder()
                    .WithContent("📭 No upcoming questions are scheduled.")
                    .AsEphemeral());
            return;
        }

        var sb = new StringBuilder();
        foreach (var q in questions)
        {
            sb.AppendLine($"**{q.ScheduledFor:yyyy-MM-dd}** (#{q.Id}) — {q.QuestionText[..Math.Min(80, q.QuestionText.Length)]}{(q.QuestionText.Length > 80 ? "…" : "")}");
        }

        var embed = new DiscordEmbedBuilder()
            .WithTitle($"📅 Upcoming Questions ({questions.Count})")
            .WithDescription(sb.ToString())
            .WithColor(new DiscordColor("#FEE75C"))
            .WithFooter("Showing next 25 unposted questions")
            .WithTimestamp(DateTimeOffset.UtcNow)
            .Build();

        await ctx.RespondAsync(new DiscordInteractionResponseBuilder().AddEmbed(embed).AsEphemeral());
    }
}
