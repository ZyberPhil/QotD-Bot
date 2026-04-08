using DSharpPlus;
using DSharpPlus.Commands;
using DSharpPlus.Interactivity;
using DSharpPlus.Interactivity.Extensions;
using Microsoft.EntityFrameworkCore;
using QotD.Bot.Configuration;
using QotD.Bot.Core;
using QotD.Bot.Data;
using QotD.Bot.Features.General;
using QotD.Bot.Features.General.Models;
using QotD.Bot.Features.Leveling;
using QotD.Bot.Features.Leveling.Data;
using QotD.Bot.Features.Leveling.Services;
using QotD.Bot.Features.QotD;
using QotD.Bot.Features.TempVoice;
using QotD.Bot.Features.MiniGames.Services;
using QotD.Bot.Features.Logging.Services;
using QotD.Bot.Services;
using Serilog;
using Serilog.Events;

// ── Bootstrap Serilog early so any startup errors are logged ──────────────────
Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateBootstrapLogger(); 

try
{
    var builder = WebApplication.CreateBuilder(args);

    // ── Module Infrastructure ──────────────────────────────────────────────────
    IBotModule[] modules = [
        new GeneralModule(),
        new LevelingModule(),
        new QotDModule(),
        new TempVoiceModule(),
        new QotD.Bot.Features.MiniGames.MiniGamesModule(),
        new QotD.Bot.Features.Logging.LoggingModule(),
        new QotD.Bot.Features.Teams.TeamsModule(),
        new QotD.Bot.Features.Birthdays.BirthdaysModule()
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

    // ── CORS ───────────────────────────────────────────────────────────────────
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
        {
            policy.AllowAnyOrigin() // Adjust this for production security
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        });
    });

    // ── API Controllers ────────────────────────────────────────────────────────
    builder.Services.AddControllers();
    builder.Services.AddMemoryCache();
    builder.Services.AddSingleton<DiscordBotLogRelay>();
    builder.Services.AddHostedService<DiscordBotLogPump>();

    // ── Serilog (full configuration from appsettings.json) ─────────────────────
    builder.Services.AddSerilog((services, loggerConfig) =>
    {
        loggerConfig
            .ReadFrom.Configuration(builder.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext();

        var relay = services.GetService<DiscordBotLogRelay>();
        if (relay is not null)
        {
            loggerConfig.WriteTo.Sink(new DiscordBotSerilogSink(relay), restrictedToMinimumLevel: LogEventLevel.Information);
        }
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
            .ConfigureEventHandlers(b => b.AddEventHandlers([
                typeof(MiniGamesEventHandler), 
                typeof(QotD.Bot.Features.Logging.Services.LogSetupEventHandler), 
                typeof(QotD.Bot.Features.Logging.Services.DiscordLoggingEventHandler), 
                typeof(QotD.Bot.Features.General.Services.HelpMenuEventHandler),
                typeof(QotD.Bot.Features.Teams.Services.TeamSetupEventHandler),
                typeof(QotD.Bot.Features.Teams.Services.TeamListEventHandler),
                typeof(QotD.Bot.Features.TempVoice.Services.TempVoiceEventHandler),
                typeof(LevelingEventHandler)
            ]))
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

    var app = builder.Build();

    // ── API Endpoints ──────────────────────────────────────────────────────────
    app.UseCors();
    app.MapControllers();

    // ── Apply EF Core Migrations at startup ────────────────────────────────────
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var levelDb = scope.ServiceProvider.GetRequiredService<LevelDatabaseContext>();
        Log.Information("Applying database migrations…");
        await db.Database.MigrateAsync();
        await levelDb.Database.MigrateAsync();
        Log.Information("Database migrations applied.");

        // ── Warm up caches and assets ──────────────────────────────────────────────
        Log.Information("Warming up minigame caches and assets…");
        app.Services.GetRequiredService<BlackjackImageService>().PreloadAllCards();
        await app.Services.GetRequiredService<MiniGamesEventHandler>().InitializeAsync();
        Log.Information("Warmup complete.");
    }

    await app.RunAsync();
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

