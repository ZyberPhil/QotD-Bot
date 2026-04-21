using DSharpPlus.Commands;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QotD.Bot.Core;
using QotD.Bot.Features.SelfRoles.Commands;
using QotD.Bot.Features.SelfRoles.Services;

namespace QotD.Bot.Features.SelfRoles;

public sealed class SelfRolesModule : IBotModule
{
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<SelfRoleService>();
    }

    public void ConfigureDiscordServices(IServiceCollection services, IServiceProvider hostProvider)
    {
    }

    public void ConfigureCommands(CommandsExtension commands)
    {
        commands.AddCommands<SelfRolesSetupCommand>();
    }
}