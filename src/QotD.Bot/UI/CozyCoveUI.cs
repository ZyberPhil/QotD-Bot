using DSharpPlus.Entities;
using DSharpPlus;

namespace QotD.Bot.UI;

/// <summary>
/// Statische Hilfsklasse für das CozyCove Bot-Design.
/// Inspiriert von professionellen, minimalistischen Layouts.
/// </summary>
public static class CozyCoveUI
{
    // "C" Icon - Kann später durch ein spezifisches Logo ersetzt werden
    public const string COZY_ICON_URL = "https://cdn.discordapp.com/attachments/1393551039537614929/1479448802539864074/GIF-2026-03-06-11-41-09.gif?ex=69b353d1&is=69b20251&hm=a658845ff89b8b7d45fb39c21ad88d00cf630251eb256e72a43d07a09afe40e4&"; 
    public static readonly DiscordColor CozyBlack = new DiscordColor("#000000");
    public static readonly DiscordColor CozyDarkGray = new DiscordColor("#1A1A1A");
    public static readonly DiscordColor CozySuccessGreen = new DiscordColor("#57F287");
    public static readonly DiscordColor CozyErrorRed = new DiscordColor("#ED4245");

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
