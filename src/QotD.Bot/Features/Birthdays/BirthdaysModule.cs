using DSharpPlus;
using DSharpPlus.Commands;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QotD.Bot.Core;
using QotD.Bot.Features.Birthdays.Commands;
using QotD.Bot.Features.Birthdays.Services;

namespace QotD.Bot.Features.Birthdays;

public sealed class BirthdaysModule : IBotModule
{
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddHostedService<BirthdayBackgroundService>();
    }

    public void ConfigureDiscordServices(IServiceCollection services, IServiceProvider hostProvider)
    {
        // Register command classes with scoped dependencies
        services.AddScoped<BirthdayCommands>();
        services.AddScoped<BirthdaySetupCommands>();
    }

    public void ConfigureCommands(CommandsExtension commands)
    {
        commands.AddCommands<BirthdayCommands>();
        commands.AddCommands<BirthdaySetupCommands>();
    }
}
