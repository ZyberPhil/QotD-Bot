using DSharpPlus;
using DSharpPlus.Commands;
using DSharpPlus.Interactivity;
using DSharpPlus.Interactivity.Extensions;
using Microsoft.EntityFrameworkCore;
using QotD.Bot.Commands;
using QotD.Bot.Configuration;
using QotD.Bot.Data;
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
                
                // Also share logging and options if needed by commands
                services.AddSingleton(s.GetRequiredService<ILogger<ConfigCommand>>());
            })
            .UseInteractivity(new InteractivityConfiguration())
            .UseCommands((_, extension) =>
            {
                extension.AddCommands<AddQuestionCommand>();
                extension.AddCommands<ListQuestionsCommand>();
                extension.AddCommands<ConfigCommand>();
            })
            .Build();
    });

    // ── Background Services ─────────────────────────────────────────────────────
    builder.Services.AddSingleton<DiscordBotService>();
    builder.Services.AddHostedService(s => s.GetRequiredService<DiscordBotService>());
    builder.Services.AddHostedService<QotDBackgroundService>();

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
