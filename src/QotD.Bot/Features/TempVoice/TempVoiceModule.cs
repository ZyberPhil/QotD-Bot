using DSharpPlus;
using DSharpPlus.Commands;
using DSharpPlus.EventArgs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QotD.Bot.Core;
using QotD.Bot.Features.TempVoice.Commands;
using QotD.Bot.Features.TempVoice.Services;

namespace QotD.Bot.Features.TempVoice;

public sealed class TempVoiceModule : IBotModule
{
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<TempVoiceEventHandler>();
    }

    public void ConfigureDiscordServices(IServiceCollection services, IServiceProvider hostProvider)
    {
        var handler = hostProvider.GetRequiredService<TempVoiceEventHandler>();
        services.AddSingleton(handler);
        services.AddSingleton<IEventHandler<VoiceStateUpdatedEventArgs>>(handler);
        services.AddSingleton<IEventHandler<ComponentInteractionCreatedEventArgs>>(handler);
    }

    public void ConfigureCommands(CommandsExtension commands)
    {
        commands.AddCommands<TempVoiceCommands>();
    }
}
