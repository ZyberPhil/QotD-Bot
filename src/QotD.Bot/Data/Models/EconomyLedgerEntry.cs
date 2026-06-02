using System.ComponentModel.DataAnnotations;

namespace QotD.Bot.Data.Models;

public enum EconomyTransactionType
{
    InitialGrant = 0,
    Credit = 1,
    Debit = 2,
    Adjustment = 3,
}

public sealed class EconomyLedgerEntry
{
    [Key]
    public long Id { get; set; }

    public ulong UserId { get; set; }

    public ulong? ActorUserId { get; set; }

    public EconomyTransactionType TransactionType { get; set; }

    public long Amount { get; set; }

    public long BalanceBefore { get; set; }

    public long BalanceAfter { get; set; }

    public string? Reason { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}