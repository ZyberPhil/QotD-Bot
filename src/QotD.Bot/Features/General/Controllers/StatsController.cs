using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QotD.Bot.Data;
using QotD.Bot.Features.General.Models;

namespace QotD.Bot.Features.General.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StatsController : ControllerBase
{
    private readonly AppDbContext _db;

    public StatsController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<BotStatsResponse>> GetStats()
    {
        var totalQuestions = await _db.Questions.CountAsync();
        var totalGuilds = await _db.GuildConfigs.CountAsync();
        var totalAnswers = await _db.GuildHistories.CountAsync();
        
        // Active MiniGames calculation
        var activeMiniGames = await _db.CountingChannels.CountAsync() + 
                             await _db.WordChainConfigs.CountAsync();

        return Ok(new BotStatsResponse(
            totalQuestions, 
            totalGuilds, 
            totalAnswers, 
            activeMiniGames
        ));
    }
}
