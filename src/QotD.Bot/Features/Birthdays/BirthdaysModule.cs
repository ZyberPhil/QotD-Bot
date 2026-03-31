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
        // No additional discord services needed for now
    }

    public void ConfigureCommands(CommandsExtension commands)
    {
        commands.AddCommands<BirthdayCommands>();
    }
}
