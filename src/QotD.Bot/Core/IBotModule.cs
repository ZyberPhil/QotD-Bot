using DSharpPlus.Commands;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace QotD.Bot.Core;

/// <summary>
/// Contract every feature module must implement.
/// A module registers its own services and commands – Program.cs never needs
/// to know about individual features.
/// </summary>
public interface IBotModule
{
    /// <summary>Register DI services (hosted services, singletons, …) for this module.</summary>
    void ConfigureServices(IServiceCollection services, IConfiguration configuration);

    /// <summary>Register services that need to be available specifically to Discord commands.</summary>
    void ConfigureDiscordServices(IServiceCollection services, IServiceProvider hostProvider);

    /// <summary>Register slash commands belonging to this module.</summary>
    void ConfigureCommands(CommandsExtension commands);
}
