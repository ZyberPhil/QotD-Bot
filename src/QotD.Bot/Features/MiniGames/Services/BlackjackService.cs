using System.Collections.Concurrent;
using QotD.Bot.Features.MiniGames.Models;

namespace QotD.Bot.Features.MiniGames.Services;

public class BlackjackService
{
    private readonly ConcurrentDictionary<(ulong GuildId, ulong UserId), BlackjackGame> _activeGames = new();
    private readonly ConcurrentDictionary<(ulong GuildId, ulong UserId), SemaphoreSlim> _userLocks = new();
    private readonly ILogger<BlackjackService> _logger;

    public BlackjackService(ILogger<BlackjackService> logger)
    {
        _logger = logger;
    }

    public SemaphoreSlim GetLock(ulong guildId, ulong userId)
    {
        return _userLocks.GetOrAdd((guildId, userId), _ => new SemaphoreSlim(1, 1));
    }

    public BlackjackGame StartGame(ulong guildId, ulong userId, int bet)
    {
        var game = new BlackjackGame(userId, bet);
        _activeGames[(guildId, userId)] = game;
        return game;
    }

    public void DealToPlayer(BlackjackGame game)
    {
        game.PlayerHand.Add(game.Deck.Draw());
    }

    public void DealToDealer(BlackjackGame game)
    {
        game.DealerHand.Add(game.Deck.Draw());
    }

    public void CheckInitialBlackjack(BlackjackGame game)
    {
        if (game.PlayerValue == 21 && game.DealerValue == 21) game.Status = GameStatus.Push;
        else if (game.PlayerValue == 21) game.Status = GameStatus.PlayerBlackjack;
        else if (game.DealerValue == 21) game.Status = GameStatus.DealerBlackjack;
    }

    public BlackjackGame? GetGame(ulong guildId, ulong userId)
    {
        if (_activeGames.TryGetValue((guildId, userId), out var game))
        {
            game.LastActivity = DateTimeOffset.UtcNow;
            return game;
        }
        return null;
    }

    public void EndGame(ulong guildId, ulong userId)
    {
        _activeGames.TryRemove((guildId, userId), out _);
    }

    public void CleanupStaleGames(TimeSpan timeout)
    {
        var now = DateTimeOffset.UtcNow;
        var staleGameKeys = _activeGames
            .Where(kvp => now - kvp.Value.LastActivity > timeout)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in staleGameKeys)
        {
            if (_activeGames.TryRemove(key, out _))
            {
                _logger.LogInformation("Cleaned up stale Blackjack game for user {UserId} in guild {GuildId}.", key.UserId, key.GuildId);
            }
        }

        // Also cleanup unused locks to prevent RAM bloat
        var unusedLockKeys = _userLocks
            .Where(kvp => kvp.Value.CurrentCount > 0) // Semaphore is not being held
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in unusedLockKeys)
        {
            // Only remove if it's still not being held (small race condition window is fine, GetOrAdd will just create a new one)
            if (_userLocks.TryGetValue(key, out var semaphore) && semaphore.CurrentCount > 0)
            {
                _userLocks.TryRemove(key, out _);
            }
        }
    }

    public void Hit(ulong guildId, ulong userId)
    {
        var game = GetGame(guildId, userId);
        if (game == null || game.Status != GameStatus.Playing) return;

        game.PlayerHand.Add(game.Deck.Draw());

        if (game.PlayerValue > 21)
        {
            game.Status = GameStatus.PlayerBust;
        }
        else if (game.PlayerValue == 21)
        {
            // Auto-stand if 21
            Stand(guildId, userId);
        }
    }

    public void Stand(ulong guildId, ulong userId)
    {
        // No longer auto-calls DealerTurn here to allow animation in UI
    }

    public bool ShouldDealerHit(BlackjackGame game)
    {
        return game.DealerValue < 17;
    }

    public void DealerHit(BlackjackGame game)
    {
        game.DealerHand.Add(game.Deck.Draw());
    }

    public void EvaluateFinalStatus(BlackjackGame game)
    {
        if (game.Status != GameStatus.Playing) return;

        if (game.DealerValue > 21)
        {
            game.Status = GameStatus.DealerBust;
        }
        else if (game.DealerValue > game.PlayerValue)
        {
            game.Status = GameStatus.DealerWon;
        }
        else if (game.DealerValue < game.PlayerValue)
        {
            game.Status = GameStatus.PlayerWon;
        }
        else
        {
            game.Status = GameStatus.Push;
        }
    }

}
