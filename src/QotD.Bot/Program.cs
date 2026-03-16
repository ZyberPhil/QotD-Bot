using DSharpPlus;
using DSharpPlus.Commands;
using DSharpPlus.Interactivity;
using DSharpPlus.Interactivity.Extensions;
using Microsoft.EntityFrameworkCore;
using QotD.Bot.Configuration;
using QotD.Bot.Core;
using QotD.Bot.Data;
using QotD.Bot.Features.General;
using QotD.Bot.Features.QotD;
using QotD.Bot.Features.TempVoice;
using QotD.Bot.Services;
using Serilog;

// ── Bootstrap Serilog early so any startup errors are logged ──────────────────
Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateBootstrapLogger(); 

try
{
    var builder = Host.CreateApplicationBuilder(args);

    // ── Module Infrastructure ──────────────────────────────────────────────────
    IBotModule[] modules = [
        new GeneralModule(),
        new QotDModule(),
        new TempVoiceModule(),
        new QotD.Bot.Features.MiniGames.MiniGamesModule()
    ];

    // ── Configuration ──────────────────────────────────────────────────────────
    builder.Configuration
           .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
           .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
           .AddEnvironmentVariables();

    // Bind strongly-typed settings
    builder.Services.Configure<DiscordSettings>(
        builder.Configuration.GetSection(DiscordSettings.SectionName));
    builder.Services.Configure<SchedulingSettings>(
        builder.Configuration.GetSection(SchedulingSettings.SectionName));

    // ── Serilog (full configuration from appsettings.json) ─────────────────────
    builder.Services.AddSerilog((services, loggerConfig) =>
    {
        loggerConfig
            .ReadFrom.Configuration(builder.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext();
    });

    // ── Database ───────────────────────────────────────────────────────────────
    builder.Services.AddDbContext<AppDbContext>(options =>
    {
        options.UseNpgsql(
            builder.Configuration.GetConnectionString("Postgres"),
            npgsql => npgsql.MigrationsAssembly("QotD.Bot"));
    });

    // ── Register Module Services ───────────────────────────────────────────────
    foreach (var module in modules)
    {
        module.ConfigureServices(builder.Services, builder.Configuration);
    }

    // ── DSharpPlus ─────────────────────────────────────────────────────────────
    var discordToken = builder.Configuration[$"{DiscordSettings.SectionName}:Token"]
        ?? throw new InvalidOperationException("Discord:Token is not configured.");

    builder.Services.AddSingleton(s =>
    {
        return DiscordClientBuilder.CreateDefault(discordToken, DiscordIntents.AllUnprivileged | DiscordIntents.MessageContents)
            .ConfigureServices(services =>
            {
                services.AddDbContext<AppDbContext>(options =>
                {
                    options.UseNpgsql(
                        builder.Configuration.GetConnectionString("Postgres"),
                        npgsql => npgsql.MigrationsAssembly("QotD.Bot"));
                });

                // Share essential singletons from the main host provider
                services.AddSingleton(s.GetRequiredService<IServiceScopeFactory>());
                services.AddSingleton(s.GetRequiredService<DiscordBotService>());

                foreach (var module in modules)
                {
                    module.ConfigureDiscordServices(services, s);
                }
            })
            .ConfigureEventHandlers(b => b.AddEventHandlers([typeof(QotD.Bot.Features.MiniGames.Services.MiniGamesEventHandler)]))
            .UseInteractivity(new InteractivityConfiguration())
            .UseCommands((_, extension) =>
            {
                foreach (var module in modules)
                {
                    module.ConfigureCommands(extension);
                }
            })
            .Build();
    });

    // ── Core Services ───────────────────────────────────────────────────────────
    builder.Services.AddSingleton<DiscordBotService>();
    builder.Services.AddHostedService(s => s.GetRequiredService<DiscordBotService>());

    var host = builder.Build();

    // ── Apply EF Core Migrations at startup ────────────────────────────────────
    using (var scope = host.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Log.Information("Applying database migrations…");
        await db.Database.MigrateAsync();
        Log.Information("Database migrations applied.");
    }

    await host.RunAsync();
}
catch (Exception ex) when (ex is not OperationCanceledException)
{
    Log.Fatal(ex, "Host terminated unexpectedly");
    return 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}

return 0;

