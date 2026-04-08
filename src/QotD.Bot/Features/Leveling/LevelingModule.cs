using DSharpPlus;
using DSharpPlus.Commands;
using DSharpPlus.EventArgs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QotD.Bot.Core;
using QotD.Bot.Features.Leveling.Commands;
using QotD.Bot.Features.Leveling.Data;
using QotD.Bot.Features.Leveling.Services;

namespace QotD.Bot.Features.Leveling;

public sealed class LevelingModule : IBotModule
{
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<LevelDatabaseContext>(options =>
        {
            options.UseNpgsql(
                configuration.GetConnectionString("Postgres")
                    ?? throw new InvalidOperationException("ConnectionStrings:Postgres is not configured."),
                npgsql => npgsql.MigrationsAssembly("QotD.Bot"));
        });

        services.AddSingleton<LevelService>();
        services.AddSingleton<LevelingEventHandler>();
    }

    public void ConfigureDiscordServices(IServiceCollection services, IServiceProvider hostProvider)
    {
        services.AddSingleton(hostProvider.GetRequiredService<LevelService>());

        var handler = hostProvider.GetRequiredService<LevelingEventHandler>();
        services.AddSingleton(handler);
        services.AddSingleton<IEventHandler<MessageCreatedEventArgs>>(handler);
    }

    public void ConfigureCommands(CommandsExtension commands)
    {
        commands.AddCommands<LevelModule>();
        commands.AddCommands<LevelingSetupCommand>();
    }
}
