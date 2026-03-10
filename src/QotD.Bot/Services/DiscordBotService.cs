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

    public DiscordBotService(
        IServiceProvider serviceProvider,
        IOptions<DiscordSettings> settings,
        ILogger<DiscordBotService> logger)
    {
        _serviceProvider = serviceProvider;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Connecting Discord bot to gateway…");
        var client = _serviceProvider.GetRequiredService<DiscordClient>();
        await client.ConnectAsync();
        _logger.LogInformation("Discord bot connected.");
    }
    
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Disconnecting Discord bot…");
        var client = _serviceProvider.GetRequiredService<DiscordClient>();
        await client.DisconnectAsync();
    }
}
