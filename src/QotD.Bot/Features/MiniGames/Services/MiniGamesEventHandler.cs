using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using QotD.Bot.Data;

namespace QotD.Bot.Features.MiniGames.Services;

public sealed class MiniGamesEventHandler : IEventHandler<MessageCreatedEventArgs>
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MiniGamesEventHandler> _logger;
    private readonly ConcurrentDictionary<ulong, SemaphoreSlim> _locks = new();

    public MiniGamesEventHandler(
        IServiceScopeFactory scopeFactory,
        ILogger<MiniGamesEventHandler> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task HandleEventAsync(DiscordClient client, MessageCreatedEventArgs e)
    {
        if (e.Author.IsBot) return;
        if (e.Guild is null) return;

        var channelId = e.Channel.Id;

        // Fast check before getting a lock
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var countingConfig = await db.CountingChannels.FirstOrDefaultAsync(c => c.ChannelId == channelId);
        if (countingConfig != null)
        {
            await HandleCountingAsync(e.Message, e.Channel, e.Guild, e.Author, countingConfig.Id);
            return;
        }

        var wordChainConfig = await db.WordChainConfigs.FirstOrDefaultAsync(c => c.ChannelId == channelId);
        if (wordChainConfig != null)
        {
            await HandleWordChainAsync(e.Message, e.Channel, e.Guild, e.Author, wordChainConfig.Id);
            return;
        }
    }

    private async Task HandleCountingAsync(DiscordMessage message, DiscordChannel channel, DiscordGuild guild, DiscordUser author, int configId)
    {
        var channelLock = _locks.GetOrAdd(channel.Id, _ => new SemaphoreSlim(1, 1));
        await channelLock.WaitAsync();

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var config = await db.CountingChannels.FindAsync(configId);
            if (config == null) return;

            var result = MathExpressionParser.TryEvaluate(message.Content);
            
            if (!result.HasValue)
            {
                // Not a valid math expression, ignore or consider as fail?
                // The prompt says "Bei falscher Zahl: Bot reagiert mit X". If it completely fails to parse, maybe ignore if it's chatting?
                // Let's assume any message in the channel must be valid.
                await FailCountingAsync(message, channel, author, db, config, message.Content, config.CurrentCount + 1);
                return;
            }

            var nextNum = config.CurrentCount + 1;
            
            if (Math.Abs(result.Value - nextNum) > 0.0001)
            {
                // Wrong number
                await FailCountingAsync(message, channel, author, db, config, message.Content, nextNum);
                return;
            }

            if (config.LastUserId == author.Id)
            {
                // User counted twice
                await FailCountingAsync(message, channel, author, db, config, "Du hast schon gezählt!", nextNum);
                return;
            }

            // Success
            config.CurrentCount = nextNum;
            config.LastUserId = author.Id;
            
            if (config.CurrentCount > config.Highscore)
            {
                config.Highscore = config.CurrentCount;
                if (config.CurrentCount % 10 == 0 || config.CurrentCount == 1) // Announce every 10 or first Highscore
                {
                    var embed = new DiscordEmbedBuilder()
                        .WithColor(new DiscordColor("#FFD700"))
                        .WithTitle("🏆 Neuer Highscore!")
                        .WithDescription($"Die Community hat einen neuen Highscore von **{config.Highscore}** erreicht!")
                        .WithTimestamp(DateTimeOffset.UtcNow);
                    await channel.SendMessageAsync(embed);
                }
            }

            await db.SaveChangesAsync();
            await message.CreateReactionAsync(DiscordEmoji.FromUnicode("✅"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling counting in channel {ChannelId}", channel.Id);
        }
        finally
        {
            channelLock.Release();
        }
    }

    private async Task FailCountingAsync(DiscordMessage message, DiscordChannel channel, DiscordUser author, AppDbContext db, Data.Models.CountingChannelConfig config, string userInput, int expectedNumber)
    {
        await message.CreateReactionAsync(DiscordEmoji.FromUnicode("❌"));

        var embed = new DiscordEmbedBuilder()
            .WithColor(new DiscordColor("#FF4444"))
            .WithTitle("💥 Zähler zurückgesetzt!")
            .WithDescription($"❌ **{author.Mention}** hat einen Fehler gemacht bei **{userInput}**!\nDie nächste Zahl wäre **{expectedNumber}** gewesen.\n\n📊 Erreichte Zahl: **{config.CurrentCount}**\n🏆 Highscore: **{config.Highscore}**")
            .WithFooter("Weiter geht's bei 1!")
            .WithTimestamp(DateTimeOffset.UtcNow);

        await channel.SendMessageAsync(embed);

        config.CurrentCount = 0;
        config.LastUserId = 0;
        await db.SaveChangesAsync();
    }

    private async Task HandleWordChainAsync(DiscordMessage message, DiscordChannel channel, DiscordGuild guild, DiscordUser author, int configId)
    {
        var channelLock = _locks.GetOrAdd(channel.Id, _ => new SemaphoreSlim(1, 1));
        await channelLock.WaitAsync();

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var config = await db.WordChainConfigs.FindAsync(configId);
            if (config == null) return;

            var input = message.Content.Trim().ToLower();

            // Validate single word with letters only (incl. German umlauts)
            if (!Regex.IsMatch(input, "^[a-zäöüß]+$"))
            {
                await FailWordChainAsync(message, channel, author, db, config, "Nur einzelne Wörter aus Buchstaben (keine Zahlen, Leerzeichen)!");
                return;
            }

            if (config.LastUserId == author.Id)
            {
                await FailWordChainAsync(message, channel, author, db, config, "Du darfst nicht zweimal hintereinander schreiben!");
                return;
            }

            if (!string.IsNullOrEmpty(config.LastWord))
            {
                var expectedStartChar = config.LastWord.Last();
                if (input.First() != expectedStartChar)
                {
                    await FailWordChainAsync(message, channel, author, db, config, $"Dein Wort muss mit **'{expectedStartChar}'** beginnen!");
                    return;
                }
            }

            var usedWords = JsonSerializer.Deserialize<HashSet<string>>(config.UsedWordsJson) ?? new HashSet<string>();
            if (usedWords.Contains(input))
            {
                await FailWordChainAsync(message, channel, author, db, config, $"Das Wort **{input}** wurde in dieser Runde schon verwendet!");
                return;
            }

            // Success
            usedWords.Add(input);
            config.UsedWordsJson = JsonSerializer.Serialize(usedWords);
            config.LastWord = input;
            config.LastUserId = author.Id;
            config.ChainLength++;

            if (config.ChainLength > config.Highscore)
            {
                config.Highscore = config.ChainLength;
                if (config.ChainLength % 10 == 0) // Announce every 10
                {
                    var embed = new DiscordEmbedBuilder()
                        .WithColor(new DiscordColor("#FFD700"))
                        .WithTitle("🏆 Neuer Highscore!")
                        .WithDescription($"Die Community hat einen neuen Highscore von **{config.Highscore}** Wörtern erreicht!")
                        .WithTimestamp(DateTimeOffset.UtcNow);
                    await channel.SendMessageAsync(embed);
                }
            }

            await db.SaveChangesAsync();
            await message.CreateReactionAsync(DiscordEmoji.FromUnicode("✅"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling word chain in channel {ChannelId}", channel.Id);
        }
        finally
        {
            channelLock.Release();
        }
    }

    private async Task FailWordChainAsync(DiscordMessage message, DiscordChannel channel, DiscordUser author, AppDbContext db, Data.Models.WordChainConfig config, string reason)
    {
        await message.CreateReactionAsync(DiscordEmoji.FromUnicode("❌"));

        var startWord = "apfel"; // We could use a random dictionary, hardcoding one for fallback.

        var embed = new DiscordEmbedBuilder()
            .WithColor(new DiscordColor("#FF4444"))
            .WithTitle("💥 Wortkette gerissen!")
            .WithDescription($"❌ **{author.Mention}** hat einen Fehler gemacht!\nGrund: *{reason}*\n\n📊 Kette gerissen bei: **{config.ChainLength}** Wörtern\n🏆 Highscore: **{config.Highscore}**")
            .WithFooter($"Neues Startwort: {startWord}")
            .WithTimestamp(DateTimeOffset.UtcNow);

        await channel.SendMessageAsync(embed);

        config.ChainLength = 0;
        config.LastUserId = 0;
        config.LastWord = startWord;
        config.UsedWordsJson = "[]";
        
        await db.SaveChangesAsync();
    }
}
