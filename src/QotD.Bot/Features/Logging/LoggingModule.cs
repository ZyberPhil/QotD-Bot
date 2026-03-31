using DSharpPlus.Commands;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QotD.Bot.Core;
using QotD.Bot.Features.Logging.Commands;

namespace QotD.Bot.Features.Logging;

public sealed class LoggingModule : IBotModule
{
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
    }

    public void ConfigureDiscordServices(IServiceCollection services, IServiceProvider hostProvider)
    {
    }

    public void ConfigureCommands(CommandsExtension commands)
    {
        commands.AddCommands<LogSetupCommand>();
    }
}
