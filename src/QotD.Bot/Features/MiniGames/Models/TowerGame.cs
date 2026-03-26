using System;
using System.Collections.Generic;

namespace QotD.Bot.Features.MiniGames.Models;

public enum TowerStatus
{
    Playing,
    CashedOut,
    Lost,
    Won
}

public class TowerFloor
{
    public int BombIndex { get; set; }
    public int SelectedTileIndex { get; set; } = -1; // -1 means not selected
    public int TileCount { get; set; } = 3;
}

public class TowerGame
{
    public Guid Id { get; } = Guid.NewGuid();
    public ulong UserId { get; }
    public int Bet { get; }
    public int CurrentFloorIndex { get; set; } = 0;
    public List<TowerFloor> Floors { get; } = new();
    
    // Example multipliers as requested: 1.40x, 2.10x, 3.50x
    public List<double> Multipliers { get; } = new() 
    {
        1.40, 2.10, 3.50, 5.00, 7.50, 10.0, 15.0, 20.0
    };
    
    public TowerStatus Status { get; set; } = TowerStatus.Playing;
    public DateTimeOffset LastActivity { get; set; } = DateTimeOffset.UtcNow;

    public TowerGame(ulong userId, int bet, int floorCount = 8)
    {
        UserId = userId;
        Bet = bet;
        var rng = new Random();
        for (int i = 0; i < floorCount; i++)
        {
            Floors.Add(new TowerFloor
            {
                TileCount = 3,
                BombIndex = rng.Next(0, 3) 
            });
        }
    }

    public double CurrentMultiplier => CurrentFloorIndex == 0 ? 1.0 : Multipliers[CurrentFloorIndex - 1];
    public int CurrentWin => (int)(Bet * CurrentMultiplier);
}
