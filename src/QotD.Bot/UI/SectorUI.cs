using DSharpPlus.Entities;
using DSharpPlus;

namespace QotD.Bot.UI;

/// <summary>
/// Statische Hilfsklasse für das Sector 0 Bot-Design.
/// Inspiriert von professionellen, minimalistischen Layouts.
/// </summary>
public static class SectorUI
{
    // "🔷" Icon - Muss eine valide HTTP/HTTPS URL sein für Discord
    public const string SECTOR_ICON_URL = "https://cdn.discordapp.com/attachments/TODO/sector_pb.png"; 
    public static readonly DiscordColor SectorBlack = new DiscordColor("#000000");
    public static readonly DiscordColor SectorDarkGray = new DiscordColor("#1A1A1A");
    public static readonly DiscordColor SectorPrimary = new DiscordColor("#5865F2");
    public static readonly DiscordColor SectorSuccessGreen = new DiscordColor("#57F287");
    public static readonly DiscordColor SectorWarning = new DiscordColor("#FAA61A");
    public static readonly DiscordColor SectorErrorRed = new DiscordColor("#ED4245");
    public static readonly DiscordColor SectorDanger = new DiscordColor("#ED4245");
    public static readonly DiscordColor SectorInfoBlue = new DiscordColor("#0099FF");
    public static readonly DiscordColor SectorGold = new DiscordColor("#FFD700");
    public static readonly DiscordColor SectorNeutralGray = new DiscordColor("#808080");

    /// <summary>
    /// Erstellt eine Basis-Embed-Konfiguration im Sector 0-Stil.
    /// </summary>
    public static DiscordEmbedBuilder CreateBaseEmbed(string? title = null, string? description = null)
    {
        var builder = new DiscordEmbedBuilder()
            .WithColor(SectorBlack)
            .WithAuthor("Sector 0", null, SECTOR_ICON_URL)
            .WithThumbnail(SECTOR_ICON_URL);

        if (!string.IsNullOrEmpty(title))
            builder.WithTitle(title);

        if (!string.IsNullOrEmpty(description))
            builder.WithDescription(description);

        return builder;
    }

    /// <summary>
    /// Fügt den analytischen Footer hinzu.
    /// </summary>
    public static DiscordEmbedBuilder WithAnalyticalFooter(this DiscordEmbedBuilder builder, int latency)
    {
        return builder.WithFooter($"Latenz: {latency}ms | Sector 0 System - Gerechtigkeit & Komfort", SECTOR_ICON_URL);
    }

    /// <summary>
    /// Fügt einen einheitlichen Standard-Footer im Sector 0-Stil hinzu.
    /// </summary>
    public static DiscordEmbedBuilder WithStandardFooter(this DiscordEmbedBuilder builder, string? metadata = null)
    {
        var baseText = "Sector 0 System";
        if (!string.IsNullOrWhiteSpace(metadata))
        {
            baseText += $" | {metadata}";
        }

        return builder.WithFooter(baseText, SECTOR_ICON_URL);
    }

    /// <summary>
    /// Fügt einen standardisierten Feature-Footer hinzu.
    /// </summary>
    public static DiscordEmbedBuilder WithFeatureFooter(this DiscordEmbedBuilder builder, string feature, string? metadata = null)
    {
        var footer = $"Sector 0 System | {feature}";
        if (!string.IsNullOrWhiteSpace(metadata))
        {
            footer += $" | {metadata}";
        }

        return builder.WithFooter(footer, SECTOR_ICON_URL);
    }

    /// <summary>
    /// Fügt einen einheitlichen Feature-Titel hinzu.
    /// </summary>
    public static DiscordEmbedBuilder WithFeatureTitle(this DiscordEmbedBuilder builder, string feature, string headline, string? emoji = null)
    {
        var title = string.IsNullOrWhiteSpace(emoji)
            ? $"{feature} - {headline}"
            : $"{emoji} {feature} - {headline}";

        return builder.WithTitle(title);
    }

    /// <summary>
    /// Erstellt ein standardisiertes Log-Embed mit Zeitstempel.
    /// </summary>
    public static DiscordEmbedBuilder CreateLogEmbed(
        string title,
        string description,
        DiscordColor color,
        string? footerMetadata = null,
        DateTimeOffset? timestamp = null)
    {
        var builder = CreateBaseEmbed(title, description)
            .WithColor(color)
            .WithTimestamp(timestamp ?? DateTimeOffset.UtcNow)
            .WithStandardFooter(footerMetadata);

        return builder;
    }

    /// <summary>
    /// Spezifisches Design für Fehler (Unregelmäßigkeiten).
    /// </summary>
    public static DiscordEmbedBuilder CreateErrorEmbed(string errorDetail)
    {
        return new DiscordEmbedBuilder()
            .WithColor(SectorErrorRed)
            .WithTitle("⚠️ Eine Unregelmäßigkeit wurde festgestellt")
            .WithDescription(errorDetail)
            .WithAuthor("Sector 0 - Analyse", null, SECTOR_ICON_URL);
    }

    /// <summary>
    /// Spezifisches Design für Erfolge (Abschluss).
    /// </summary>
    public static DiscordEmbedBuilder CreateSuccessEmbed(string message, string? title = null)
    {
        return CreateBaseEmbed(title ?? "✅ Vorgang abgeschlossen", message)
            .WithColor(SectorSuccessGreen);
    }

    /// <summary>
    /// Spezifisches Design für Informationen.
    /// </summary>
    public static DiscordEmbedBuilder CreateInfoEmbed(string message, string? title = null)
    {
        return CreateBaseEmbed(title ?? "ℹ️ Information", message)
            .WithColor(SectorDarkGray);
    }
}
