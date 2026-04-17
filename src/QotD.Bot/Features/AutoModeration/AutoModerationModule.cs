using DSharpPlus;
using DSharpPlus.Commands;
using DSharpPlus.EventArgs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QotD.Bot.Core;
using QotD.Bot.Features.AutoModeration.Commands;
using QotD.Bot.Features.AutoModeration.Services;

namespace QotD.Bot.Features.AutoModeration;

public sealed class AutoModerationModule : IBotModule
{
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<AutoModerationService>();
        services.AddSingleton<AutoModerationEventHandler>();
    }

    public void ConfigureDiscordServices(IServiceCollection services, IServiceProvider hostProvider)
    {
        var handler = hostProvider.GetRequiredService<AutoModerationEventHandler>();
        services.AddSingleton(handler);
        services.AddSingleton<IEventHandler<GuildMemberAddedEventArgs>>(handler);
        services.AddSingleton<IEventHandler<MessageCreatedEventArgs>>(handler);
    }

    public void ConfigureCommands(CommandsExtension commands)
    {
        commands.AddCommands<AutoModerationCommands>();
    }
}
