namespace QotD.Bot.Features.MiniGames.Models;

public enum GameStatus
{
    Playing,
    PlayerBust,
    DealerBust,
    PlayerBlackjack,
    DealerBlackjack,
    PlayerWon,
    DealerWon,
    Push
}

public class BlackjackGame
{
    public Guid Id { get; } = Guid.NewGuid();
    public ulong UserId { get; }
    public Deck Deck { get; } = new();
    public List<Card> PlayerHand { get; } = new();
    public List<Card> DealerHand { get; } = new();
    public GameStatus Status { get; set; } = GameStatus.Playing;
    public DateTimeOffset LastActivity { get; set; } = DateTimeOffset.UtcNow;

    public int Bet { get; }

    public BlackjackGame(ulong userId, int bet)
    {
        UserId = userId;
        Bet = bet;
    }

    public int GetHandValue(List<Card> hand)
    {
        int value = hand.Sum(c => c.GetValue());
        int aces = hand.Count(c => c.Rank == CardRank.Ace);

        while (value > 21 && aces > 0)
        {
            value -= 10;
            aces--;
        }

        return value;
    }

    public int PlayerValue => GetHandValue(PlayerHand);
    public int DealerValue => GetHandValue(DealerHand);
}
