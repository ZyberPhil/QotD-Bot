using DSharpPlus.Commands;
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
    }

    public void ConfigureDiscordServices(IServiceCollection services, IServiceProvider hostProvider)
    {
        services.AddSingleton<BlackjackService>();
        services.AddSingleton<BlackjackImageService>();
    }

    public void ConfigureCommands(CommandsExtension commands)
    {
        commands.AddCommands<MiniGamesCommand>();
    }
}
