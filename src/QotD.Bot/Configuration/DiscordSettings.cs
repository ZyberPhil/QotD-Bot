namespace QotD.Bot.Configuration;

/// <summary>Settings for the Discord bot, loaded from appsettings.json / environment variables.</summary>
public sealed class DiscordSettings
{
    public const string SectionName = "Discord";

    /// <summary>The bot token from the Discord Developer Portal.</summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>The Guild (server) ID for guild-registered slash commands.</summary>
    public ulong GuildId { get; set; }

    /// <summary>The Channel ID where the daily question will be posted.</summary>
    public ulong ChannelId { get; set; }
}
