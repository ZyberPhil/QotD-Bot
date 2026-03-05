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
    private readonly DiscordClient _client;
    private readonly DiscordSettings _settings;
    private readonly ILogger<DiscordBotService> _logger;

    public DiscordBotService(
        DiscordClient client,
        IOptions<DiscordSettings> settings,
        ILogger<DiscordBotService> logger)
    {
        _client = client;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Connecting Discord bot to gateway…");
        await _client.ConnectAsync();
        _logger.LogInformation("Discord bot connected. Serving guild {GuildId}, channel {ChannelId}.",
            _settings.GuildId, _settings.ChannelId);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Disconnecting Discord bot…");
        await _client.DisconnectAsync();
    }
}
