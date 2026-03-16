using System.ComponentModel;
using DSharpPlus.Commands;
using DSharpPlus.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QotD.Bot.Data;
using QotD.Bot.Data.Models;
using DescriptionAttribute = System.ComponentModel.DescriptionAttribute;

namespace QotD.Bot.Features.MiniGames.Commands;

[DSharpPlus.Commands.Command("minigames")]
[Description("Mini-Games Konfiguration")]
public class MiniGamesCommand
{
    private readonly IServiceScopeFactory _scopeFactory;

    public MiniGamesCommand(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    [DSharpPlus.Commands.Command("counting")]
    [Description("Zähl-Kanal Befehle")]
    public class CountingCommands
    {
        private readonly IServiceScopeFactory _scopeFactory;

        public CountingCommands(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
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

        public WordChainCommands(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
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
}
