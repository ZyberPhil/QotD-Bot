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

using QotD.Bot.Features.MiniGames.Models;
using QotD.Bot.UI;

namespace QotD.Bot.Features.MiniGames.Services;

public sealed class MiniGamesEventHandler : 
    IEventHandler<MessageCreatedEventArgs>,
    IEventHandler<ComponentInteractionCreatedEventArgs>
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MiniGamesEventHandler> _logger;
    private readonly BlackjackService _blackjackService;
    private readonly BlackjackImageService _imageService;
    private readonly ConcurrentDictionary<ulong, SemaphoreSlim> _locks = new();

    public MiniGamesEventHandler(
        IServiceScopeFactory scopeFactory,
        ILogger<MiniGamesEventHandler> logger,
        BlackjackService blackjackService,
        BlackjackImageService imageService)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _blackjackService = blackjackService;
        _imageService = imageService;
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

    public async Task HandleEventAsync(DiscordClient client, ComponentInteractionCreatedEventArgs e)
    {
        var id = e.Id;
        if (!id.StartsWith("bj_")) return;

        // Custom ID format: bj_action_userId
        var parts = id.Split('_');
        if (parts.Length < 3) return;

        var action = parts[1];
        if (!ulong.TryParse(parts[2], out var userId)) return;

        // Only the player who started the game can interact
        if (e.User.Id != userId)
        {
            await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder()
                .WithContent("Dies ist nicht dein Spiel!")
                .AsEphemeral(true));
            return;
        }

        if (action == "play" && parts.Length > 3 && parts[2] == "again")
        {
             // Fix logic: parts[1] is "play", parts[2] is "again", parts[3] is userId
             // But my UI uses "bj_play_again_{userId}"
             // So parts[1] is "play", parts[2] is "again", parts[3] is userId. Correct.
        }
        
        // Let's re-parse for play again specifically
        if (id.StartsWith("bj_play_again_"))
        {
             if (ulong.TryParse(id.Substring("bj_play_again_".Length), out var pid))
             {
                 var g = _blackjackService.StartGame(pid);
                 var img = _imageService.CreateGameTableImage(g.PlayerHand, g.DealerHand, true);
                 var resp = BlackjackUI.BuildResponse(g, img);
                 await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.UpdateMessage, resp);
                 return;
             }
        }

        var activeGame = _blackjackService.GetGame(userId);
        if (activeGame == null)
        {
            await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.UpdateMessage, new DiscordInteractionResponseBuilder()
                .WithContent("Kein aktives Spiel gefunden. Starte ein neues mit `/minigames blackjack play`."));
            return;
        }

        switch (action)
        {
            case "hit":
                _blackjackService.Hit(userId);
                break;
            case "stand":
                _blackjackService.Stand(userId);
                break;
        }

        var hideDealer = activeGame.Status == GameStatus.Playing;
        var tableImage = _imageService.CreateGameTableImage(activeGame.PlayerHand, activeGame.DealerHand, hideDealer);
        var update = BlackjackUI.BuildResponse(activeGame, tableImage);
        
        await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.UpdateMessage, update);

        if (activeGame.Status != GameStatus.Playing)
        {
            _blackjackService.EndGame(userId);
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

        var embed = new DiscordEmbedBuilder()
            .WithColor(new DiscordColor("#FF4444"))
            .WithTitle("💥 Wortkette gerissen!")
            .WithDescription($"❌ **{author.Mention}** hat einen Fehler gemacht!\nGrund: *{reason}*\n\n📊 Kette gerissen bei: **{config.ChainLength}** Wörtern\n🏆 Highscore: **{config.Highscore}**")
            .WithFooter("Die Kette wurde zurückgesetzt. Du kannst jetzt mit jedem beliebigen Wort beginnen!")
            .WithTimestamp(DateTimeOffset.UtcNow);

        await channel.SendMessageAsync(embed);

        config.ChainLength = 0;
        config.LastUserId = 0;
        config.LastWord = null;
        config.UsedWordsJson = "[]";
        
        await db.SaveChangesAsync();
    }
}
