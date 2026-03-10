using DSharpPlus.Entities;
using DSharpPlus;

namespace QotD.Bot.UI;

/// <summary>
/// Statische Hilfsklasse für das Lawliet (L) Bot-Design.
/// </summary>
public static class LawlietUI
{
    public const string L_ICON_URL = "https://raw.githubusercontent.com/L-Lawliet-Discord/Assets/main/L_icon.png"; // Beispiel URL für das Old English L
    public static readonly DiscordColor LawlietBlack = new DiscordColor("#000000");

    /// <summary>
    /// Erstellt eine Basis-Embed-Konfiguration im Lawliet-Stil.
    /// </summary>
    public static DiscordEmbedBuilder CreateBaseEmbed(string? title = null, string? description = null)
    {
        var builder = new DiscordEmbedBuilder()
            .WithColor(LawlietBlack)
            .WithAuthor("L", null, L_ICON_URL)
            .WithThumbnail(L_ICON_URL);

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
        return builder.WithFooter($"Latenz: {latency}ms | Gerechtigkeit wird siegen", L_ICON_URL);
    }

    /// <summary>
    /// Spezifisches Design für Fehler (Unregelmäßigkeiten).
    /// </summary>
    public static DiscordEmbedBuilder CreateErrorEmbed(string errorDetail)
    {
        return new DiscordEmbedBuilder()
            .WithColor(new DiscordColor("#1A1A1A")) // Sehr dunkles Grau für Fehler
            .WithTitle("⚠️ Eine Unregelmäßigkeit wurde festgestellt")
            .WithDescription(errorDetail)
            .WithAuthor("L - Analyse", null, L_ICON_URL);
    }

    /// <summary>
    /// Spezifisches Design für Erfolge (Abschluss).
    /// </summary>
    public static DiscordEmbedBuilder CreateSuccessEmbed(string message)
    {
        return CreateBaseEmbed("✅ Vorgang abgeschlossen", message);
    }
}
