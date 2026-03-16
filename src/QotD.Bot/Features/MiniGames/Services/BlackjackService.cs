using System.Collections.Concurrent;
using QotD.Bot.Features.MiniGames.Models;

namespace QotD.Bot.Features.MiniGames.Services;

public class BlackjackService
{
    private readonly ConcurrentDictionary<ulong, BlackjackGame> _activeGames = new();

    public BlackjackGame StartGame(ulong userId)
    {
        var game = new BlackjackGame(userId);
        
        // Initial deal
        game.PlayerHand.Add(game.Deck.Draw());
        game.DealerHand.Add(game.Deck.Draw());
        game.PlayerHand.Add(game.Deck.Draw());
        game.DealerHand.Add(game.Deck.Draw());

        // Check for immediate blackjacks
        if (game.PlayerValue == 21 && game.DealerValue == 21) game.Status = GameStatus.Push;
        else if (game.PlayerValue == 21) game.Status = GameStatus.PlayerBlackjack;
        else if (game.DealerValue == 21) game.Status = GameStatus.DealerBlackjack;

        _activeGames[userId] = game;
        return game;
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
        var game = GetGame(userId);
        if (game == null || game.Status != GameStatus.Playing) return;

        DealerTurn(game);
    }

    private void DealerTurn(BlackjackGame game)
    {
        // Dealer must hit until 17 or higher
        while (game.DealerValue < 17)
        {
            game.DealerHand.Add(game.Deck.Draw());
        }

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
