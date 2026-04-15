using DSharpPlus.Entities;
using DSharpPlus;

namespace QotD.Bot.UI;

/// <summary>
/// Statische Hilfsklasse für das CozyCove Bot-Design.
/// Inspiriert von professionellen, minimalistischen Layouts.
/// </summary>
public static class CozyCoveUI
{
    // "C" Icon - Muss eine valide HTTP/HTTPS URL sein für Discord
    public const string COZY_ICON_URL = "https://cdn.discordapp.com/attachments/1399075190591193118/1481574657390936144/cozy-cove.gif?ex=69b3cf6c&is=69b27dec&hm=09ec233e821632e74a10cf5542edf9103ddb7a8075459ddbb1753ffcef156307&"; 
    public static readonly DiscordColor CozyBlack = new DiscordColor("#000000");
    public static readonly DiscordColor CozyDarkGray = new DiscordColor("#1A1A1A");
    public static readonly DiscordColor CozyPrimary = new DiscordColor("#5865F2");
    public static readonly DiscordColor CozySuccessGreen = new DiscordColor("#57F287");
    public static readonly DiscordColor CozyWarning = new DiscordColor("#FAA61A");
    public static readonly DiscordColor CozyErrorRed = new DiscordColor("#ED4245");
    public static readonly DiscordColor CozyDanger = new DiscordColor("#ED4245");
    public static readonly DiscordColor CozyInfoBlue = new DiscordColor("#0099FF");
    public static readonly DiscordColor CozyGold = new DiscordColor("#FFD700");
    public static readonly DiscordColor CozyNeutralGray = new DiscordColor("#808080");

    /// <summary>
    /// Erstellt eine Basis-Embed-Konfiguration im CozyCove-Stil.
    /// </summary>
    public static DiscordEmbedBuilder CreateBaseEmbed(string? title = null, string? description = null)
    {
        var builder = new DiscordEmbedBuilder()
            .WithColor(CozyBlack)
            .WithAuthor("CozyCove", null, COZY_ICON_URL)
            .WithThumbnail(COZY_ICON_URL);

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
        return builder.WithFooter($"Latenz: {latency}ms | CozyCove System - Gerechtigkeit & Komfort", COZY_ICON_URL);
    }

    /// <summary>
    /// Fügt einen einheitlichen Standard-Footer im CozyCove-Stil hinzu.
    /// </summary>
    public static DiscordEmbedBuilder WithStandardFooter(this DiscordEmbedBuilder builder, string? metadata = null)
    {
        var baseText = "CozyCove System";
        if (!string.IsNullOrWhiteSpace(metadata))
        {
            baseText += $" | {metadata}";
        }

        return builder.WithFooter(baseText, COZY_ICON_URL);
    }

    /// <summary>
    /// Fügt einen standardisierten Feature-Footer hinzu.
    /// </summary>
    public static DiscordEmbedBuilder WithFeatureFooter(this DiscordEmbedBuilder builder, string feature, string? metadata = null)
    {
        var footer = $"CozyCove System | {feature}";
        if (!string.IsNullOrWhiteSpace(metadata))
        {
            footer += $" | {metadata}";
        }

        return builder.WithFooter(footer, COZY_ICON_URL);
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
            .WithColor(CozyErrorRed)
            .WithTitle("⚠️ Eine Unregelmäßigkeit wurde festgestellt")
            .WithDescription(errorDetail)
            .WithAuthor("CozyCove - Analyse", null, COZY_ICON_URL);
    }

    /// <summary>
    /// Spezifisches Design für Erfolge (Abschluss).
    /// </summary>
    public static DiscordEmbedBuilder CreateSuccessEmbed(string message, string? title = null)
    {
        return CreateBaseEmbed(title ?? "✅ Vorgang abgeschlossen", message)
            .WithColor(CozySuccessGreen);
    }

    /// <summary>
    /// Spezifisches Design für Informationen.
    /// </summary>
    public static DiscordEmbedBuilder CreateInfoEmbed(string message, string? title = null)
    {
        return CreateBaseEmbed(title ?? "ℹ️ Information", message)
            .WithColor(CozyDarkGray);
    }
}
