using DSharpPlus.Commands;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QotD.Bot.Core;
using QotD.Bot.Features.QotD.Commands;
using QotD.Bot.Features.QotD.Services;

namespace QotD.Bot.Features.QotD;

/// <summary>
/// Feature module that wires up everything related to Question of the Day.
/// Add this to the modules array in Program.cs and the feature is fully active.
/// </summary>
public sealed class QotDModule : IBotModule
{
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<QotDPostingService>();
        services.AddHostedService<QotDBackgroundService>();
    }

    public void ConfigureDiscordServices(IServiceCollection services, IServiceProvider hostProvider)
    {
        services.AddSingleton(hostProvider.GetRequiredService<QotDPostingService>());
        services.AddSingleton(hostProvider.GetRequiredService<ILogger<QotDCommand>>());
    }

    public void ConfigureCommands(CommandsExtension commands)
    {
        commands.AddCommands<QotDCommand>();
    }
}
