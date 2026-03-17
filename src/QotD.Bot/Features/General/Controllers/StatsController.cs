using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using QotD.Bot.Data;
using QotD.Bot.Features.General.Models;

namespace QotD.Bot.Features.General.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StatsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IMemoryCache _cache;
    private const string StatsCacheKey = "BotStats";

    public StatsController(AppDbContext db, IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
    }

    [HttpGet]
    public async Task<ActionResult<BotStatsResponse>> GetStats()
    {
        if (_cache.TryGetValue(StatsCacheKey, out BotStatsResponse? cachedStats))
        {
            return Ok(cachedStats);
        }

        var totalQuestions = await _db.Questions.AsNoTracking().CountAsync();
        var totalGuilds = await _db.GuildConfigs.AsNoTracking().CountAsync();
        var totalAnswers = await _db.GuildHistories.AsNoTracking().CountAsync();
        
        // Active MiniGames calculation
        var activeMiniGames = await _db.CountingChannels.AsNoTracking().CountAsync() + 
                             await _db.WordChainConfigs.AsNoTracking().CountAsync();

        var stats = new BotStatsResponse(
            totalQuestions, 
            totalGuilds, 
            totalAnswers, 
            activeMiniGames
        );

        _cache.Set(StatsCacheKey, stats, TimeSpan.FromSeconds(30));

        return Ok(stats);
    }
}
