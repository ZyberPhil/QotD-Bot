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

using QotD.Bot.Features.Economy.Services;
using QotD.Bot.Features.MiniGames.Models;
using QotD.Bot.UI;

namespace QotD.Bot.Features.MiniGames.Services;

public enum MiniGameType { Counting, WordChain }
public record struct MiniGameChannelInfo(MiniGameType Type, int ConfigId);

public sealed class MiniGamesEventHandler : 
    IEventHandler<MessageCreatedEventArgs>,
    IEventHandler<ComponentInteractionCreatedEventArgs>
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MiniGamesEventHandler> _logger;
    private readonly BlackjackService _blackjackService;
    private readonly BlackjackImageService _imageService;
    private readonly TowerService _towerService;
    private readonly ConcurrentDictionary<ulong, SemaphoreSlim> _locks = new();
    private static readonly ConcurrentDictionary<ulong, MiniGameChannelInfo> _minigameChannels = new();
    private static readonly Regex _wordRegex = new("^[a-zäöüß]+$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public MiniGamesEventHandler(
        IServiceScopeFactory scopeFactory,
        ILogger<MiniGamesEventHandler> logger,
        BlackjackService blackjackService,
        BlackjackImageService imageService,
        TowerService towerService)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _blackjackService = blackjackService;
        _imageService = imageService;
        _towerService = towerService;
    }

    public async Task InitializeAsync()
    {
        _logger.LogInformation("Initializing MiniGames channel cache…");
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var countingChannels = await db.CountingChannels.AsNoTracking().Select(c => new { c.ChannelId, c.Id }).ToListAsync();
        foreach (var c in countingChannels) 
        {
            _minigameChannels[c.ChannelId] = new MiniGameChannelInfo(MiniGameType.Counting, c.Id);
            _logger.LogInformation("Cached Counting channel: {ChannelId} (ConfigId: {Id})", c.ChannelId, c.Id);
        }

        var wordChainChannels = await db.WordChainConfigs.AsNoTracking().Select(c => new { c.ChannelId, c.Id }).ToListAsync();
        foreach (var c in wordChainChannels)
        {
            _minigameChannels[c.ChannelId] = new MiniGameChannelInfo(MiniGameType.WordChain, c.Id);
            _logger.LogInformation("Cached WordChain channel: {ChannelId} (ConfigId: {Id})", c.ChannelId, c.Id);
        }
        
        _logger.LogInformation("MiniGames cache initialized with {Count} counting and {Count2} wordchain channels.", countingChannels.Count, wordChainChannels.Count);
    }

    public void RegisterChannel(ulong channelId, MiniGameType type, int configId) 
        => _minigameChannels[channelId] = new MiniGameChannelInfo(type, configId);
        
    public void UnregisterChannel(ulong channelId) => _minigameChannels.TryRemove(channelId, out _);

    public void CleanupUnusedLocks()
    {
        var unusedChannelIds = _locks
            .Where(kvp => kvp.Value.CurrentCount > 0)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var id in unusedChannelIds)
        {
            if (_locks.TryGetValue(id, out var semaphore) && semaphore.CurrentCount > 0)
            {
                _locks.TryRemove(id, out _);
            }
        }
    }

    public async Task HandleEventAsync(DiscordClient client, MessageCreatedEventArgs e)
    {
        if (e.Author.IsBot) return;
        if (e.Guild is null) return;

        var channelId = e.Channel.Id;
        
        // Fast exit for the 99% of normal chat messages
        if (!_minigameChannels.TryGetValue(channelId, out var info)) return;

        _logger.LogInformation("Detected minigame message in {ChannelId} (Type: {Type})", channelId, info.Type);

        // Zero-Discovery: We already know the game type and ID from our cache!
        if (info.Type == MiniGameType.Counting)
        {
            await HandleCountingAsync(e.Message, e.Channel, e.Guild, e.Author, info.ConfigId);
        }
        else if (info.Type == MiniGameType.WordChain)
        {
            await HandleWordChainAsync(e.Message, e.Channel, e.Guild, e.Author, info.ConfigId);
        }
    }

    public async Task HandleEventAsync(DiscordClient client, ComponentInteractionCreatedEventArgs e)
    {
        var id = e.Id;
        if (id.StartsWith("tower_"))
        {
            await HandleTowerInteractionAsync(e);
            return;
        }
        if (!id.StartsWith("bj_")) return;

        // Use a per-user lock for all Blackjack interactions to prevent race conditions
        var userLock = _blackjackService.GetLock(e.User.Id);
        await userLock.WaitAsync();

        try
        {
            // Handle Play Again separately as it has a different ID format
            if (id.StartsWith("bj_play_again_"))
            {
                 if (ulong.TryParse(id.Substring("bj_play_again_".Length), out var pid))
                 {
                     // Only the player who started the original game can click "Play Again"
                     if (e.User.Id != pid)
                     {
                         await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder()
                             .WithContent("Dies ist nicht dein Spiel! Starte ein eigenes mit `/minigames blackjack play`.")
                             .AsEphemeral(true));
                         return;
                     }

                     // Custom ID format: bj_play_again_userId_bet
                     var partsPlayAgain = id.Split('_');
                     int playAgainBet = 0;
                     if (partsPlayAgain.Length >= 5)
                     {
                         int.TryParse(partsPlayAgain[4], out playAgainBet);
                     }

                     if (playAgainBet > 0)
                     {
                         using var scope = _scopeFactory.CreateScope();
                         var economy = scope.ServiceProvider.GetRequiredService<EconomyService>();
                         var economyResult = await economy.RemoveCoinsAsync(pid, playAgainBet);
                         if (!economyResult.IsApiAvailable)
                         {
                             playAgainBet = 0;
                             await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder()
                                 .WithContent("⚠️ Die Economy-API ist derzeit offline. Das Spiel startet ohne Echtgeld-Einsatz! (Just for Fun)")
                                 .AsEphemeral(true));
                         }
                         else if (!economyResult.IsSuccess)
                         {
                             await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder()
                                 .WithContent($"❌ {economyResult.ErrorMessage}")
                                 .AsEphemeral(true));
                             return;
                         }
                     }

                     var g = _blackjackService.StartGame(pid, playAgainBet);
                     
                     // Initial deal animation for Play Again
                     _blackjackService.DealToPlayer(g);
                     var img1 = _imageService.CreateGameTableImage(g.PlayerHand, g.DealerHand, true);
                     // Hide buttons during animation to prevent early "Hit" clicks
                     await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.UpdateMessage, BlackjackUI.BuildResponse(g, img1, showButtons: false));

                     await Task.Delay(1000);
                     _blackjackService.DealToDealer(g);
                     var img2 = _imageService.CreateGameTableImage(g.PlayerHand, g.DealerHand, true);
                     await e.Interaction.EditOriginalResponseAsync(BlackjackUI.BuildResponse(g, img2, showButtons: false).ToWebhookBuilder());

                     await Task.Delay(1000);
                     _blackjackService.DealToPlayer(g);
                     var img3 = _imageService.CreateGameTableImage(g.PlayerHand, g.DealerHand, true);
                     await e.Interaction.EditOriginalResponseAsync(BlackjackUI.BuildResponse(g, img3, showButtons: false).ToWebhookBuilder());

                     await Task.Delay(1000);
                     _blackjackService.DealToDealer(g);
                     _blackjackService.CheckInitialBlackjack(g);
                     
                     var img4 = _imageService.CreateGameTableImage(g.PlayerHand, g.DealerHand, g.Status == GameStatus.Playing);
                     // Final step of animation: show buttons if still playing
                     await e.Interaction.EditOriginalResponseAsync(BlackjackUI.BuildResponse(g, img4, showButtons: true).ToWebhookBuilder());

                     if (g.Status != GameStatus.Playing)
                     {
                         await ProcessBlackjackPayoutAsync(g);
                         _blackjackService.EndGame(pid);
                     }
                     return;
                 }
            }

            // Custom ID format: bj_action_userId
            var parts = id.Split('_');
            if (parts.Length < 3) return;

            var action = parts[1];
            if (!ulong.TryParse(parts[parts.Length - 1], out var userId)) return;

            // Only the player who started the game can interact
            if (e.User.Id != userId)
            {
                await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder()
                    .WithContent("Dies ist nicht dein Spiel!")
                    .AsEphemeral(true));
                return;
            }

            var activeGame = _blackjackService.GetGame(userId);
            if (activeGame == null)
            {
                // CRITICAL: Use ephemeral message so we don't overwrite a finished game board
                await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder()
                    .WithContent("Kein aktives Spiel gefunden. Starte ein neues mit `/minigames blackjack play`.")
                    .AsEphemeral(true));
                return;
            }

            // Safety check: if the game is already finished (e.g. animation still running or multiple clicks), 
            // don't try to Hit or Stand again.
            if (activeGame.Status != GameStatus.Playing)
            {
                await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder()
                    .WithContent("Dieses Spiel ist bereits beendet.")
                    .AsEphemeral(true));
                return;
            }

            switch (action)
            {
                case "hit":
                    _blackjackService.DealToPlayer(activeGame);
                    
                    // Show the new card
                    var hitImg = _imageService.CreateGameTableImage(activeGame.PlayerHand, activeGame.DealerHand, true);
                    // Hide buttons briefly during hit reveal? 
                    // Actually let's just show them, but hit is usually fast.
                    var hitResp = BlackjackUI.BuildResponse(activeGame, hitImg);
                    await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.UpdateMessage, hitResp);
                    
                    // Check if busted
                    if (activeGame.PlayerValue > 21)
                    {
                        await Task.Delay(1000);
                        activeGame.Status = GameStatus.PlayerBust;
                        
                        var bustImg = _imageService.CreateGameTableImage(activeGame.PlayerHand, activeGame.DealerHand, false);
                        var bustResp = BlackjackUI.BuildResponse(activeGame, bustImg);
                        await e.Interaction.EditOriginalResponseAsync(bustResp.ToWebhookBuilder());
                        await ProcessBlackjackPayoutAsync(activeGame);
                        _blackjackService.EndGame(userId);
                    }
                    return;
                case "stand":
                    // Send initial reveal (show dealer's hidden card)
                    var revealImg = _imageService.CreateGameTableImage(activeGame.PlayerHand, activeGame.DealerHand, false);
                    var revealResp = BlackjackUI.BuildResponse(activeGame, revealImg, showButtons: false);
                    await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.UpdateMessage, revealResp);

                    // Animated Dealer Turn
                    while (_blackjackService.ShouldDealerHit(activeGame))
                    {
                        await Task.Delay(1000);
                        _blackjackService.DealerHit(activeGame);
                        var dealerTableImg = _imageService.CreateGameTableImage(activeGame.PlayerHand, activeGame.DealerHand, false);
                        var dealerUpdate = BlackjackUI.BuildResponse(activeGame, dealerTableImg, showButtons: false);
                        await e.Interaction.EditOriginalResponseAsync(dealerUpdate.ToWebhookBuilder());
                    }

                    await Task.Delay(1000);
                    _blackjackService.EvaluateFinalStatus(activeGame);
                    
                    // Final update with result
                    var finalImg = _imageService.CreateGameTableImage(activeGame.PlayerHand, activeGame.DealerHand, false);
                    var finalUpdate = BlackjackUI.BuildResponse(activeGame, finalImg, showButtons: true);
                    await e.Interaction.EditOriginalResponseAsync(finalUpdate.ToWebhookBuilder());

                    if (activeGame.Status != GameStatus.Playing)
                    {
                        await ProcessBlackjackPayoutAsync(activeGame);
                        _blackjackService.EndGame(userId);
                    }
                    return;
            }

            var hideDealer = activeGame.Status == GameStatus.Playing;
            var tableImage = _imageService.CreateGameTableImage(activeGame.PlayerHand, activeGame.DealerHand, hideDealer);
            var update = BlackjackUI.BuildResponse(activeGame, tableImage);
            
            await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.UpdateMessage, update);

            if (activeGame.Status != GameStatus.Playing)
            {
                await ProcessBlackjackPayoutAsync(activeGame);
                _blackjackService.EndGame(userId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling Blackjack interaction {InteractionId} for user {UserId}", id, e.User.Id);
            try 
            {
               await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder()
                    .WithContent("Ein technischer Fehler ist aufgetreten.")
                    .AsEphemeral(true));
            }
            catch { /* Ignore secondary errors during error reporting */ }
        }
        finally
        {
            userLock.Release();
        }
    }

    private async Task ProcessBlackjackPayoutAsync(BlackjackGame game)
    {
        if (game.Bet <= 0) return;

        int winAmount = 0;
        if (game.Status == GameStatus.PlayerWon || game.Status == GameStatus.DealerBust)
        {
            winAmount = game.Bet * 2;
        }
        else if (game.Status == GameStatus.PlayerBlackjack)
        {
            winAmount = game.Bet + (int)(game.Bet * 1.5);
        }
        else if (game.Status == GameStatus.Push)
        {
            winAmount = game.Bet;
        }

        if (winAmount > 0)
        {
            using var scope = _scopeFactory.CreateScope();
            var economy = scope.ServiceProvider.GetRequiredService<EconomyService>();
            await economy.AddCoinsAsync(game.UserId, winAmount);
        }
    }

    private async Task HandleTowerInteractionAsync(ComponentInteractionCreatedEventArgs e)
    {
        var id = e.Id;
        var parts = id.Split('_'); // tower_pick_userId_tileIndex OR tower_cashout_userId OR tower_play_again_userId
        if (parts.Length < 3) return;

        var action = parts[1];
        if (!Guid.TryParse(parts[2], out var gameId) && !ulong.TryParse(parts[2], out var _))
        {
            // Fallback for play_again which has format tower_play_again_userId
            if (action == "play" && parts.Length >= 4 && parts[2] == "again")
            {
                if (!ulong.TryParse(parts[3], out var playAgainUserId)) return;
                
                if (e.User.Id != playAgainUserId)
                {
                    await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder()
                        .WithContent("Dies ist nicht dein Spiel!").AsEphemeral(true));
                    return;
                }

                int playAgainBet = 0;
                if (parts.Length >= 5)
                {
                    int.TryParse(parts[4], out playAgainBet);
                }

                if (playAgainBet > 0)
                {
                    using var scope = _scopeFactory.CreateScope();
                    var economy = scope.ServiceProvider.GetRequiredService<EconomyService>();
                    var economyResult = await economy.RemoveCoinsAsync(playAgainUserId, playAgainBet);
                    if (!economyResult.IsApiAvailable)
                    {
                        playAgainBet = 0;
                        await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder()
                            .WithContent("⚠️ Die Economy-API ist derzeit offline. Das Spiel startet ohne Echtgeld-Einsatz! (Just for Fun)")
                            .AsEphemeral(true));
                    }
                    else if (!economyResult.IsSuccess)
                    {
                        await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder()
                            .WithContent($"❌ {economyResult.ErrorMessage}")
                            .AsEphemeral(true));
                        return;
                    }
                }

                var newGame = _towerService.StartGame(playAgainUserId, playAgainBet);
                await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.UpdateMessage, TowerUI.BuildResponse(newGame));
                return;
            }
            return;
        }

        var activeGame = _towerService.GetGame(e.User.Id);

        // Active game safety checks
        if (activeGame == null || activeGame.Id.ToString() != parts[2] && parts[2] != e.User.Id.ToString())
        {
            await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder()
                .WithContent("Dies ist nicht dein Spiel oder das Spiel ist abgelaufen!").AsEphemeral(true));
            return;
        }

        if (activeGame.Status != TowerStatus.Playing)
        {
            await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder()
                .WithContent("Dieses Spiel ist bereits beendet.").AsEphemeral(true));
            return;
        }

        var userLock = _towerService.GetLock(e.User.Id);
        await userLock.WaitAsync();

        try
        {
            // Re-check status inside lock
            if (activeGame.Status != TowerStatus.Playing) return;

            if (action == "pick" && parts.Length >= 4)
            {
                if (int.TryParse(parts[3], out var tileIndex))
                {
                    _towerService.PickTile(activeGame, tileIndex);
                }
            }
            else if (action == "cashout")
            {
                _towerService.CashOut(activeGame);
            }

            // Always update UI
            var response = TowerUI.BuildResponse(activeGame);
            await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.UpdateMessage, response);

            if (activeGame.Status != TowerStatus.Playing)
            {
                await ProcessTowerPayoutAsync(activeGame);
                _towerService.EndGame(e.User.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling Tower interaction {InteractionId}", id);
        }
        finally
        {
            userLock.Release();
        }
    }

    private async Task ProcessTowerPayoutAsync(TowerGame game)
    {
        if (game.Bet <= 0) return;
        
        if (game.Status == TowerStatus.CashedOut || game.Status == TowerStatus.Won)
        {
            if (game.CurrentWin > 0)
            {
                using var scope = _scopeFactory.CreateScope();
                var economy = scope.ServiceProvider.GetRequiredService<EconomyService>();
                await economy.AddCoinsAsync(game.UserId, game.CurrentWin);
            }
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
            if (!_wordRegex.IsMatch(input))
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
