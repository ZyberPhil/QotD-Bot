using DSharpPlus;
using DSharpPlus.Commands;
using DSharpPlus.EventArgs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QotD.Bot.Core;
using QotD.Bot.Features.MiniGames.Commands;
using QotD.Bot.Features.MiniGames.Services;

namespace QotD.Bot.Features.MiniGames;

/// <summary>
/// Feature module that wires up everything related to Mini-Games (Counting Channel, Word Chain).
/// </summary>
public sealed class MiniGamesModule : IBotModule
{
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<BlackjackService>();
        services.AddSingleton<BlackjackImageService>();
        services.AddSingleton<TowerService>();
        services.AddSingleton<MiniGamesEventHandler>();
        services.AddHostedService<BlackjackCleanupService>();
    }

    public void ConfigureDiscordServices(IServiceCollection services, IServiceProvider hostProvider)
    {
        // Resolve from the main (host) container to ensure we share the SAME instance/cache
        var handler = hostProvider.GetRequiredService<MiniGamesEventHandler>();
        services.AddSingleton(handler);
        services.AddSingleton<IEventHandler<MessageCreatedEventArgs>>(handler);
        services.AddSingleton<IEventHandler<ComponentInteractionCreatedEventArgs>>(handler);

        services.AddSingleton(hostProvider.GetRequiredService<BlackjackService>());
        services.AddSingleton(hostProvider.GetRequiredService<BlackjackImageService>());
        services.AddSingleton(hostProvider.GetRequiredService<TowerService>());
    }

    public void ConfigureCommands(CommandsExtension commands)
    {
        commands.AddCommands<CountingCommands>();
        commands.AddCommands<WordChainCommands>();
        commands.AddCommands<BlackjackCommands>();
        commands.AddCommands<TowerCommands>();
    }
}
