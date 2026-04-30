using DSharpPlus;
using DSharpPlus.Commands;
using DSharpPlus.EventArgs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QotD.Bot.Core;
using QotD.Bot.Features.Logging.Commands;
using QotD.Bot.Features.Logging.Services;

namespace QotD.Bot.Features.Logging;

public sealed class LoggingModule : IBotModule
{
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<DiscordLoggingEventHandler>();
        services.AddSingleton<LogSetupEventHandler>();
        services.AddSingleton<DiscordBotLogRelay>();
        services.AddHostedService<DiscordBotLogPump>();
    }

    public void ConfigureDiscordServices(IServiceCollection services, IServiceProvider hostProvider)
    {
        var loggingHandler = hostProvider.GetRequiredService<DiscordLoggingEventHandler>();
        services.AddSingleton(loggingHandler);
        services.AddSingleton<IEventHandler<MessageDeletedEventArgs>>(loggingHandler);
        services.AddSingleton<IEventHandler<MessageUpdatedEventArgs>>(loggingHandler);
        services.AddSingleton<IEventHandler<GuildMemberAddedEventArgs>>(loggingHandler);
        services.AddSingleton<IEventHandler<GuildMemberRemovedEventArgs>>(loggingHandler);
        services.AddSingleton<IEventHandler<VoiceStateUpdatedEventArgs>>(loggingHandler);

        var logSetupHandler = hostProvider.GetRequiredService<LogSetupEventHandler>();
        services.AddSingleton(logSetupHandler);
        services.AddSingleton<IEventHandler<ComponentInteractionCreatedEventArgs>>(logSetupHandler);
    }

    public void ConfigureCommands(CommandsExtension commands)
    {
        commands.AddCommands<LogSetupCommand>();
    }
}
