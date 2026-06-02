using DSharpPlus.Commands;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QotD.Bot.Core;
using QotD.Bot.Features.Economy.Commands;
using QotD.Bot.Features.Economy.Services;

namespace QotD.Bot.Features.Economy;

public sealed class EconomyModule : IBotModule
{
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IEconomyService, EconomyService>();
    }

    public void ConfigureDiscordServices(IServiceCollection services, IServiceProvider hostProvider)
    {
        services.AddScoped<BalanceCommand>();
        services.AddScoped<LedgerCommand>();
        services.AddScoped<CoinAdminCommands>();
        services.AddSingleton<IEconomyService>(hostProvider.GetRequiredService<IEconomyService>());
    }

    public void ConfigureCommands(CommandsExtension commands)
    {
        commands.AddCommands<BalanceCommand>();
        commands.AddCommands<LedgerCommand>();
        commands.AddCommands<CoinAdminCommands>();
    }
}