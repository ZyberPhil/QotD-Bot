using System.ComponentModel;
using DSharpPlus.Commands;
using DSharpPlus.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QotD.Bot.Data;
using QotD.Bot.Data.Models;
using QotD.Bot.Features.MiniGames.Models;
using QotD.Bot.Features.MiniGames.Services;
using QotD.Bot.UI;
using DescriptionAttribute = System.ComponentModel.DescriptionAttribute;

namespace QotD.Bot.Features.MiniGames.Commands;

[DSharpPlus.Commands.Command("counting")]
[Description("Zähl-Kanal Befehle")]
public class CountingCommands
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly MiniGamesEventHandler _eventHandler;

    public CountingCommands(IServiceScopeFactory scopeFactory, MiniGamesEventHandler eventHandler)
    {
        _scopeFactory = scopeFactory;
        _eventHandler = eventHandler;
    }

    [DSharpPlus.Commands.Command("setup")]
    [Description("Setzt den Zähl-Kanal")]
    public async ValueTask SetupAsync(CommandContext ctx, 
        [Description("Der Kanal für das Zähl-Spiel")] DiscordChannel channel)
    {
        if (ctx.Guild is null) return;
        // TODO: require admin permissions
        
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var existing = await db.CountingChannels.FirstOrDefaultAsync(c => c.GuildId == ctx.Guild.Id);
        if (existing != null)
        {
            _eventHandler.UnregisterChannel(existing.ChannelId);
            existing.ChannelId = channel.Id;
        }
        else
        {
            db.CountingChannels.Add(new CountingChannelConfig
            {
                GuildId = ctx.Guild.Id,
                ChannelId = channel.Id,
            });
        }

        await db.SaveChangesAsync();
        var configId = existing?.Id ?? (db.Entry(existing ?? db.CountingChannels.Local.First(c => c.ChannelId == channel.Id)).Entity.Id);
        _eventHandler.RegisterChannel(channel.Id, MiniGameType.Counting, configId);
        await ctx.RespondAsync($"Der Zähl-Kanal wurde auf {channel.Mention} gesetzt!");
    }
    
    [DSharpPlus.Commands.Command("reset")]
    [Description("Manueller Reset des Zählers")]
    public async ValueTask ResetAsync(CommandContext ctx)
    {
        if (ctx.Guild is null) return;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var existing = await db.CountingChannels.FirstOrDefaultAsync(c => c.GuildId == ctx.Guild.Id);
        if (existing != null)
        {
            existing.CurrentCount = 0;
            existing.LastUserId = 0;
            await db.SaveChangesAsync();
            await ctx.RespondAsync("Der Zähler wurde manuell zurückgesetzt.");
        }
        else
        {
            await ctx.RespondAsync("Es ist kein Zähl-Kanal konfiguriert.");
        }
    }
}

[DSharpPlus.Commands.Command("wordchain")]
[Description("Wortketten Befehle")]
public class WordChainCommands
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly MiniGamesEventHandler _eventHandler;

    public WordChainCommands(IServiceScopeFactory scopeFactory, MiniGamesEventHandler eventHandler)
    {
        _scopeFactory = scopeFactory;
        _eventHandler = eventHandler;
    }

    [DSharpPlus.Commands.Command("setup")]
    [Description("Setzt den Wortketten-Kanal")]
    public async ValueTask SetupAsync(CommandContext ctx, 
        [Description("Der Kanal für das Wortketten-Spiel")] DiscordChannel channel)
    {
        if (ctx.Guild is null) return;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var existing = await db.WordChainConfigs.FirstOrDefaultAsync(c => c.GuildId == ctx.Guild.Id);
        if (existing != null)
        {
            _eventHandler.UnregisterChannel(existing.ChannelId);
            existing.ChannelId = channel.Id;
        }
        else
        {
            db.WordChainConfigs.Add(new WordChainConfig
            {
                GuildId = ctx.Guild.Id,
                ChannelId = channel.Id,
            });
        }

        await db.SaveChangesAsync();
        var configId = existing?.Id ?? (db.Entry(existing ?? db.WordChainConfigs.Local.First(c => c.ChannelId == channel.Id)).Entity.Id);
        _eventHandler.RegisterChannel(channel.Id, MiniGameType.WordChain, configId);
        await ctx.RespondAsync($"Der Wortketten-Kanal wurde auf {channel.Mention} gesetzt!");
    }
    
    [DSharpPlus.Commands.Command("reset")]
    [Description("Manueller Reset der Wortkette")]
    public async ValueTask ResetAsync(CommandContext ctx)
    {
        if (ctx.Guild is null) return;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var existing = await db.WordChainConfigs.FirstOrDefaultAsync(c => c.GuildId == ctx.Guild.Id);
        if (existing != null)
        {
            existing.ChainLength = 0;
            existing.LastUserId = 0;
            existing.UsedWordsJson = "[]";
            existing.LastWord = null;
            await db.SaveChangesAsync();
            await ctx.RespondAsync("Die Wortkette wurde manuell zurückgesetzt.");
        }
        else
        {
            await ctx.RespondAsync("Es ist kein Wortketten-Kanal konfiguriert.");
        }
    }
}

public class BlackjackCommands
{
    private readonly BlackjackService _blackjackService;
    private readonly BlackjackImageService _imageService;

    public BlackjackCommands(BlackjackService blackjackService, BlackjackImageService imageService)
    {
        _blackjackService = blackjackService;
        _imageService = imageService;
    }

    [DSharpPlus.Commands.Command("blackjack")]
    [Description("Startet eine Runde Blackjack")]
    public async ValueTask PlayAsync(CommandContext ctx)
    {
        var userLock = _blackjackService.GetLock(ctx.User.Id);
        await userLock.WaitAsync();

        try
        {
            var game = _blackjackService.StartGame(ctx.User.Id);
            
            // First response: Player's first card
            _blackjackService.DealToPlayer(game);
            var img1 = _imageService.CreateGameTableImage(game.PlayerHand, game.DealerHand, true);
            // Hide buttons during initial deal animation
            await ctx.RespondAsync(BlackjackUI.BuildResponse(game, img1, showButtons: false));

            // 2nd card: Dealer (hidden)
            await Task.Delay(1000);
            _blackjackService.DealToDealer(game);
            var img2 = _imageService.CreateGameTableImage(game.PlayerHand, game.DealerHand, true);
            await ctx.EditResponseAsync(BlackjackUI.BuildResponse(game, img2, showButtons: false).ToWebhookBuilder());

            // 3rd card: Player
            await Task.Delay(1000);
            _blackjackService.DealToPlayer(game);
            var img3 = _imageService.CreateGameTableImage(game.PlayerHand, game.DealerHand, true);
            await ctx.EditResponseAsync(BlackjackUI.BuildResponse(game, img3, showButtons: false).ToWebhookBuilder());

            // 4th card: Dealer
            await Task.Delay(1000);
            _blackjackService.DealToDealer(game);
            
            // check for immediate blackjack
            _blackjackService.CheckInitialBlackjack(game);

            var img4 = _imageService.CreateGameTableImage(game.PlayerHand, game.DealerHand, game.Status == GameStatus.Playing);
            // Show buttons now that initial deal is complete
            await ctx.EditResponseAsync(BlackjackUI.BuildResponse(game, img4, showButtons: true).ToWebhookBuilder());

            if (game.Status != GameStatus.Playing)
            {
                _blackjackService.EndGame(ctx.User.Id);
            }
        }
        catch (Exception)
        {
            // We don't have an ILogger here directly, but we can use the context's provider if needed or just catch for safety.
            // In this case, let's just ensure we respond.
            await ctx.RespondAsync("Ein technischer Fehler ist aufgetreten.");
        }
        finally
        {
            userLock.Release();
        }
    }
}
