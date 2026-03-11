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
public sealed class DiscordBotService(
    IServiceProvider serviceProvider,
    IOptions<DiscordSettings> settings,
    ILogger<DiscordBotService> logger) : IHostedService
{
    private readonly DiscordSettings _settings = settings.Value;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Connecting Discord bot to gateway…");
        var client = serviceProvider.GetRequiredService<DiscordClient>();
        await client.ConnectAsync();
        logger.LogInformation("Discord bot connected.");
    }
    
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Disconnecting Discord bot…");
        var client = serviceProvider.GetRequiredService<DiscordClient>();
        await client.DisconnectAsync();
    }
}
