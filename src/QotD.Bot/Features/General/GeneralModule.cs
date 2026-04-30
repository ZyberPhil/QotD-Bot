using DSharpPlus.Commands;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QotD.Bot.Core;
using QotD.Bot.Features.General.Commands;

namespace QotD.Bot.Features.General;

/// <summary>
/// Module for shared/general bot commands like help and investigate.
/// </summary>
public sealed class GeneralModule : IBotModule
{
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        // No extra services needed for general commands
    }

    public void ConfigureDiscordServices(IServiceCollection services, IServiceProvider hostProvider)
    {
        // Register command classes with dependencies
        services.AddScoped<InvestigateCommand>();
    }

    public void ConfigureCommands(CommandsExtension commands)
    {
        commands.AddCommands<HelpCommand>();
        commands.AddCommands<InvestigateCommand>();
    }
}
