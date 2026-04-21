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
    services.AddScoped<TeamActivityService>();
    services.AddScoped<ModerationService>();
    services.AddHostedService<TeamActivityBackgroundService>();
    }

    public void ConfigureDiscordServices(IServiceCollection services, IServiceProvider hostProvider)
    {
        services.AddTransient<TeamListService>();
        services.AddScoped<TeamActivityService>();
        services.AddScoped<ModerationService>();
    }

    public void ConfigureCommands(CommandsExtension commands)
    {
        commands.AddCommands<TeamSetupCommand>();
        commands.AddCommands<TeamCommand>();
        commands.AddCommands<ModerationCommand>();
    }
}
