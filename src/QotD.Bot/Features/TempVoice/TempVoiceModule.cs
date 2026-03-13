using DSharpPlus.Commands;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QotD.Bot.Core;

namespace QotD.Bot.Features.TempVoice;

/// <summary>
/// Skeleton module for the TempVoice feature.
/// Shows how easily new features can be added by implementing IBotModule.
/// </summary>
public sealed class TempVoiceModule : IBotModule
{
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        // Add services here later:
        // services.AddHostedService<TempVoiceService>();
    }

    public void ConfigureDiscordServices(IServiceCollection services, IServiceProvider hostProvider)
    {
        // Add services here later
    }

    public void ConfigureCommands(CommandsExtension commands)
    {
        // Add commands here later:
        // commands.AddCommands<TempVoiceCommand>();
    }
}
