namespace QotD.Bot.Features.MiniGames.Models;

public enum CardSuit
{
    Clubs,
    Diamonds,
    Hearts,
    Spades
}

public enum CardRank
{
    Two = 2,
    Three = 3,
    Four = 4,
    Five = 5,
    Six = 6,
    Seven = 7,
    Eight = 8,
    Nine = 9,
    Ten = 10,
    Jack,
    Queen,
    King,
    Ace
}

public record Card(CardSuit Suit, CardRank Rank)
{
    public int GetValue()
    {
        return Rank switch
        {
            CardRank.Ace => 11,
            CardRank.Jack or CardRank.Queen or CardRank.King => 10,
            _ => (int)Rank
        };
    }

    public string GetFileName()
    {
        string rankStr = Rank switch
        {
            CardRank.Ace => "A",
            CardRank.Two => "2",
            CardRank.Three => "3",
            CardRank.Four => "4",
            CardRank.Five => "5",
            CardRank.Six => "6",
            CardRank.Seven => "7",
            CardRank.Eight => "8",
            CardRank.Nine => "9",
            CardRank.Ten => "10",
            CardRank.Jack => "J",
            CardRank.Queen => "Q",
            CardRank.King => "K",
            _ => Rank.ToString()
        };
        return $"{rankStr}_of_{Suit}.svg";
    }

    public override string ToString() => $"{Rank} of {Suit}";
}

public class Deck
{
    private readonly List<Card> _cards = new();
    private readonly Random _random = new();

    public Deck()
    {
        foreach (CardSuit suit in Enum.GetValues<CardSuit>())
        {
            foreach (CardRank rank in Enum.GetValues<CardRank>())
            {
                _cards.Add(new Card(suit, rank));
            }
        }
        Shuffle();
    }

    public void Shuffle()
    {
        int n = _cards.Count;
        while (n > 1)
        {
            n--;
            int k = _random.Next(n + 1);
            (_cards[k], _cards[n]) = (_cards[n], _cards[k]);
        }
    }

    public Card Draw()
    {
        if (_cards.Count == 0) throw new InvalidOperationException("Deck is empty");
        var card = _cards[0];
        _cards.RemoveAt(0);
        return card;
    }

    public int Remaining => _cards.Count;
}
