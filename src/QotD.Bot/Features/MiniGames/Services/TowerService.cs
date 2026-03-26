using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;
using QotD.Bot.Features.MiniGames.Models;

namespace QotD.Bot.Features.MiniGames.Services;

public class TowerService
{
    private readonly ConcurrentDictionary<ulong, TowerGame> _activeGames = new();
    private readonly ConcurrentDictionary<ulong, SemaphoreSlim> _userLocks = new();
    private readonly ILogger<TowerService> _logger;

    public TowerService(ILogger<TowerService> logger)
    {
        _logger = logger;
    }

    public SemaphoreSlim GetLock(ulong userId)
    {
        return _userLocks.GetOrAdd(userId, _ => new SemaphoreSlim(1, 1));
    }

    public TowerGame StartGame(ulong userId, int bet)
    {
        var game = new TowerGame(userId, bet, floorCount: 8);
        _activeGames[userId] = game;
        return game;
    }

    public TowerGame? GetGame(ulong userId)
    {
        if (_activeGames.TryGetValue(userId, out var game))
        {
            game.LastActivity = DateTimeOffset.UtcNow;
            return game;
        }
        return null;
    }

    public void PickTile(TowerGame game, int tileIndex)
    {
        if (game.Status != TowerStatus.Playing) return;

        var floor = game.Floors[game.CurrentFloorIndex];
        floor.SelectedTileIndex = tileIndex;

        if (tileIndex == floor.BombIndex)
        {
            game.Status = TowerStatus.Lost;
        }
        else
        {
            game.CurrentFloorIndex++;
            if (game.CurrentFloorIndex >= game.Floors.Count)
            {
                game.Status = TowerStatus.Won;
            }
        }
    }

    public void CashOut(TowerGame game)
    {
        if (game.Status != TowerStatus.Playing || game.CurrentFloorIndex == 0) return;
        game.Status = TowerStatus.CashedOut;
    }

    public void EndGame(ulong userId)
    {
        _activeGames.TryRemove(userId, out _);
    }

    public void CleanupStaleGames(TimeSpan timeout)
    {
        var now = DateTimeOffset.UtcNow;
        var staleUserIds = _activeGames
            .Where(kvp => now - kvp.Value.LastActivity > timeout)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var userId in staleUserIds)
        {
            if (_activeGames.TryRemove(userId, out _))
            {
                _logger.LogInformation("Cleaned up stale Tower game for user {UserId}.", userId);
            }
        }

        var unusedLockUserIds = _userLocks
            .Where(kvp => kvp.Value.CurrentCount > 0)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var userId in unusedLockUserIds)
        {
            if (_userLocks.TryGetValue(userId, out var semaphore) && semaphore.CurrentCount > 0)
            {
                _userLocks.TryRemove(userId, out _);
            }
        }
    }
}
