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
    Task<EconomyResult> AddCoinsAsync(ulong userId, int amount);
    Task<EconomyResult> RemoveCoinsAsync(ulong userId, int amount);
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

    public async Task<EconomyResult> AddCoinsAsync(ulong userId, int amount)
    {
        if (amount <= 0) return EconomyResult.Success();

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync();
            var account = await GetOrCreateAccountAsync(db, userId);

            checked
            {
                account.Balance += amount;
            }

            account.UpdatedAtUtc = DateTimeOffset.UtcNow;
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

    public async Task<EconomyResult> RemoveCoinsAsync(ulong userId, int amount)
    {
        if (amount <= 0) return EconomyResult.Success();

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync();
            var account = await GetOrCreateAccountAsync(db, userId);

            if (account.Balance < amount)
            {
                return EconomyResult.Failure($"Nicht genug Coins. Aktuell verfügbar: {account.Balance}.");
            }

            account.Balance -= amount;
            account.UpdatedAtUtc = DateTimeOffset.UtcNow;
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
        await db.SaveChangesAsync();

        _logger.LogInformation("Created economy account for user {UserId} with starter balance {StarterBalance}.", userId, _starterBalance);
        return account;
    }
}
