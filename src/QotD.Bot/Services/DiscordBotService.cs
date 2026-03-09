using DSharpPlus;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QotD.Bot.Configuration;

namespace QotD.Bot.Services;

/// <summary>
/// Hosted service that connects the <see cref="DiscordClient"/> to the gateway
/// and gracefully disconnects when the host is stopping.
/// </summary>
public sealed class DiscordBotService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly DiscordSettings _settings;
    private readonly ILogger<DiscordBotService> _logger;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TemplateSessionService _sessionService;

    public DiscordBotService(
        IServiceProvider serviceProvider,
        IOptions<DiscordSettings> settings,
        IServiceScopeFactory scopeFactory,
        TemplateSessionService sessionService,
        ILogger<DiscordBotService> logger)
    {
        _serviceProvider = serviceProvider;
        _settings = settings.Value;
        _scopeFactory = scopeFactory;
        _sessionService = sessionService;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Connecting Discord bot to gateway…");
        var client = _serviceProvider.GetRequiredService<DiscordClient>();
        await client.ConnectAsync();
        _logger.LogInformation("Discord bot connected.");
    }
    
    public async Task OnMessageCreatedAsync(DiscordClient sender, DSharpPlus.EventArgs.MessageCreatedEventArgs e)
    {
        if (e.Author.IsBot || e.Guild == null) return;
        
        if (_sessionService.IsInSession(e.Author.Id, e.Guild.Id))
        {
            _logger.LogInformation("Template session detected for user {UserId} in Guild {GuildId}.", e.Author.Id, e.Guild.Id);
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<Data.AppDbContext>();
                
                var config = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(
                    db.GuildConfigs, g => g.GuildId == e.Guild.Id);
                
                if (config == null)
                {
                    config = new Data.Models.GuildConfig { GuildId = e.Guild.Id };
                    db.GuildConfigs.Add(config);
                }
                
                config.MessageTemplate = e.Message.Content;
                await db.SaveChangesAsync();
                
                _sessionService.EndSession(e.Author.Id, e.Guild.Id);
                await e.Message.RespondAsync("✅ Das neue Template wurde gespeichert! Du kannst es mit `/config-qotd template-show` ansehen.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update template from message.");
                await e.Message.RespondAsync("❌ Fehler beim Speichern des Templates.");
            }
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Disconnecting Discord bot…");
        var client = _serviceProvider.GetRequiredService<DiscordClient>();
        await client.DisconnectAsync();
    }
}
