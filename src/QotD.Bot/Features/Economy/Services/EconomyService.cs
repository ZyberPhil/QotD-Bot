using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using QotD.Bot.Data;
using QotD.Bot.Data.Models;
using QotD.Bot.Features.Economy.Models;

namespace QotD.Bot.Features.Economy.Services;

public interface IEconomyService
{
    Task<EconomyResult> GetBalanceAsync(ulong userId);
    Task<EconomyResult> AddCoinsAsync(ulong userId, int amount, ulong? actorUserId = null, string? reason = null);
    Task<EconomyResult> RemoveCoinsAsync(ulong userId, int amount, ulong? actorUserId = null, string? reason = null);
    Task<EconomyResult> SetBalanceAsync(ulong userId, long amount, ulong? actorUserId = null, string? reason = null);
    Task<IReadOnlyList<EconomyLedgerEntry>> GetLedgerAsync(ulong userId, int limit = 10);
}

public sealed class EconomyService : IEconomyService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EconomyService> _logger;
    private readonly long _starterBalance;

    public EconomyService(IServiceScopeFactory scopeFactory, IConfiguration config, ILogger<EconomyService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _starterBalance = config.GetValue<long?>("Economy:StarterBalance") ?? 1000;
    }

    public async Task<EconomyResult> GetBalanceAsync(ulong userId)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var account = await GetOrCreateAccountAsync(db, userId);
        return EconomyResult.Success(account.Balance);
    }

    public async Task<EconomyResult> AddCoinsAsync(ulong userId, int amount, ulong? actorUserId = null, string? reason = null)
    {
        if (amount <= 0) return EconomyResult.Success();

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync();
            var account = await GetOrCreateAccountAsync(db, userId);
            var balanceBefore = account.Balance;

            checked
            {
                account.Balance += amount;
            }

            account.UpdatedAtUtc = DateTimeOffset.UtcNow;
            db.EconomyLedgerEntries.Add(new EconomyLedgerEntry
            {
                UserId = userId,
                ActorUserId = actorUserId,
                TransactionType = EconomyTransactionType.Credit,
                Amount = amount,
                BalanceBefore = balanceBefore,
                BalanceAfter = account.Balance,
                Reason = reason,
                CreatedAtUtc = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
            return EconomyResult.Success(account.Balance);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add coins for user {UserId}.", userId);
            return EconomyResult.Failure("Guthaben konnte nicht gutgeschrieben werden.");
        }
    }

    public async Task<EconomyResult> RemoveCoinsAsync(ulong userId, int amount, ulong? actorUserId = null, string? reason = null)
    {
        if (amount <= 0) return EconomyResult.Success();

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync();
            var account = await GetOrCreateAccountAsync(db, userId);
            var balanceBefore = account.Balance;

            if (account.Balance < amount)
            {
                return EconomyResult.Failure($"Nicht genug Coins. Aktuell verfügbar: {account.Balance}.");
            }

            account.Balance -= amount;
            account.UpdatedAtUtc = DateTimeOffset.UtcNow;
            db.EconomyLedgerEntries.Add(new EconomyLedgerEntry
            {
                UserId = userId,
                ActorUserId = actorUserId,
                TransactionType = EconomyTransactionType.Debit,
                Amount = -amount,
                BalanceBefore = balanceBefore,
                BalanceAfter = account.Balance,
                Reason = reason,
                CreatedAtUtc = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
            return EconomyResult.Success(account.Balance);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove coins for user {UserId}.", userId);
            return EconomyResult.Failure("Coins konnten nicht abgezogen werden.");
        }
    }

    public async Task<EconomyResult> SetBalanceAsync(ulong userId, long amount, ulong? actorUserId = null, string? reason = null)
    {
        if (amount < 0)
        {
            return EconomyResult.Failure("Der Kontostand kann nicht negativ sein.");
        }

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync();
            var account = await GetOrCreateAccountAsync(db, userId);
            var balanceBefore = account.Balance;

            account.Balance = amount;
            account.UpdatedAtUtc = DateTimeOffset.UtcNow;
            db.EconomyLedgerEntries.Add(new EconomyLedgerEntry
            {
                UserId = userId,
                ActorUserId = actorUserId,
                TransactionType = EconomyTransactionType.Adjustment,
                Amount = amount - balanceBefore,
                BalanceBefore = balanceBefore,
                BalanceAfter = amount,
                Reason = reason,
                CreatedAtUtc = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
            return EconomyResult.Success(account.Balance);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set balance for user {UserId}.", userId);
            return EconomyResult.Failure("Guthaben konnte nicht gesetzt werden.");
        }
    }

    public async Task<IReadOnlyList<EconomyLedgerEntry>> GetLedgerAsync(ulong userId, int limit = 10)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        limit = Math.Clamp(limit, 1, 50);

        return await db.EconomyLedgerEntries
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.Id)
            .Take(limit)
            .ToListAsync();
    }

    private async Task<EconomyAccount> GetOrCreateAccountAsync(AppDbContext db, ulong userId)
    {
        var account = await db.EconomyAccounts.FirstOrDefaultAsync(x => x.UserId == userId);
        if (account is not null)
        {
            return account;
        }

        account = new EconomyAccount
        {
            UserId = userId,
            Balance = _starterBalance,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        };

        db.EconomyAccounts.Add(account);
        db.EconomyLedgerEntries.Add(new EconomyLedgerEntry
        {
            UserId = userId,
            TransactionType = EconomyTransactionType.InitialGrant,
            Amount = _starterBalance,
            BalanceBefore = 0,
            BalanceAfter = _starterBalance,
            Reason = "Starter balance",
            CreatedAtUtc = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        _logger.LogInformation("Created economy account for user {UserId} with starter balance {StarterBalance}.", userId, _starterBalance);
        return account;
    }
}
