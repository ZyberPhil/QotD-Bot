using System.Collections.Concurrent;
using QotD.Bot.Features.MiniGames.Models;

namespace QotD.Bot.Features.MiniGames.Services;

public class BlackjackService
{
    private readonly ConcurrentDictionary<ulong, BlackjackGame> _activeGames = new();
    private readonly ConcurrentDictionary<ulong, SemaphoreSlim> _userLocks = new();

    public SemaphoreSlim GetLock(ulong userId)
    {
        return _userLocks.GetOrAdd(userId, _ => new SemaphoreSlim(1, 1));
    }

    public BlackjackGame StartGame(ulong userId)
    {
        var game = new BlackjackGame(userId);
        _activeGames[userId] = game;
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

    public BlackjackGame? GetGame(ulong userId)
    {
        if (_activeGames.TryGetValue(userId, out var game))
        {
            game.LastActivity = DateTimeOffset.UtcNow;
            return game;
        }
        return null;
    }

    public void EndGame(ulong userId)
    {
        _activeGames.TryRemove(userId, out _);
    }

    public void Hit(ulong userId)
    {
        var game = GetGame(userId);
        if (game == null || game.Status != GameStatus.Playing) return;

        game.PlayerHand.Add(game.Deck.Draw());

        if (game.PlayerValue > 21)
        {
            game.Status = GameStatus.PlayerBust;
        }
        else if (game.PlayerValue == 21)
        {
            // Auto-stand if 21
            Stand(userId);
        }
    }

    public void Stand(ulong userId)
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
