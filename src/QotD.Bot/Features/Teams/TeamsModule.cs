using DSharpPlus.Commands;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QotD.Bot.Core;
using QotD.Bot.Features.Teams.Commands;
using QotD.Bot.Features.Teams.Services;

namespace QotD.Bot.Features.Teams;

public sealed class TeamsModule : IBotModule
{
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
    }

    public void ConfigureDiscordServices(IServiceCollection services, IServiceProvider hostProvider)
    {
        services.AddTransient<TeamListService>();
    }

    public void ConfigureCommands(CommandsExtension commands)
    {
        commands.AddCommands<TeamSetupCommand>();
    }
}
