using DSharpPlus;
using DSharpPlus.Commands;
using DSharpPlus.EventArgs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QotD.Bot.Core;
using QotD.Bot.Features.LinkModeration.Commands;
using QotD.Bot.Features.LinkModeration.Services;

namespace QotD.Bot.Features.LinkModeration;

public sealed class LinkModerationModule : IBotModule
{
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<LinkModerationService>();
        services.AddSingleton<LinkModerationEventHandler>();
    }

    public void ConfigureDiscordServices(IServiceCollection services, IServiceProvider hostProvider)
    {
        services.AddScoped<LinkModerationService>();

        var handler = hostProvider.GetRequiredService<LinkModerationEventHandler>();
        services.AddSingleton(handler);
        services.AddSingleton<IEventHandler<MessageCreatedEventArgs>>(handler);
    }

    public void ConfigureCommands(CommandsExtension commands)
    {
        commands.AddCommands<LinkFilterCommands>();
    }
}
