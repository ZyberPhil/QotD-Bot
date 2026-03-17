namespace QotD.Bot.Features.General.Models;

public record BotStatsResponse(
    int TotalQuestions,
    int TotalGuilds,
    int TotalAnswers,
    int ActiveMiniGames
);
