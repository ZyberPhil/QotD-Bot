using DSharpPlus;
using DSharpPlus.Commands;
using DSharpPlus.Commands.Processors.SlashCommands;
using DSharpPlus.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using QotD.Bot.Data;
using QotD.Bot.Data.Models;
using System.ComponentModel;

namespace QotD.Bot.Commands;

[Command("config-qotd")]
[Description("Configure Question of the Day settings for this server (Admin only).")]
public sealed class ConfigCommand
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ConfigCommand> _logger;

    public ConfigCommand(IServiceScopeFactory scopeFactory, ILogger<ConfigCommand> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    [Command("channel")]
    [Description("Set the channel where questions will be posted.")]
    public async ValueTask SetChannelAsync(CommandContext ctx, [Description("The target channel")] DiscordChannel channel)
    {
        if (!await CheckPermissionsAsync(ctx)) return;

        if (channel.Type != DiscordChannelType.Text)
        {
            await ctx.RespondAsync("❌ Please select a text channel.");
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var config = await GetOrCreateConfigAsync(db, ctx.Guild!.Id);
        config.ChannelId = channel.Id;

        await db.SaveChangesAsync();
        await ctx.RespondAsync($"✅ Question of the Day will now be posted in {channel.Mention}.");
    }

    [Command("time")]
    [Description("Set the time when the question will be posted (HH:mm).")]
    public async ValueTask SetTimeAsync(CommandContext ctx, [Description("Time in HH:mm format (e.g., 08:30)")] string time)
    {
        if (!await CheckPermissionsAsync(ctx)) return;

        if (!TimeOnly.TryParseExact(time, "HH:mm", out var postTime))
        {
            await ctx.RespondAsync("❌ Invalid time format. Please use HH:mm (e.g., 07:00, 22:30).");
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var config = await GetOrCreateConfigAsync(db, ctx.Guild!.Id);
        config.PostTime = postTime;

        await db.SaveChangesAsync();
        await ctx.RespondAsync($"✅ Question of the Day will now be posted at **{postTime:HH:mm}** ({config.Timezone}).");
    }

    private static async Task<bool> CheckPermissionsAsync(CommandContext ctx)
    {
        if (ctx.Guild is null)
        {
            await ctx.RespondAsync("❌ This command can only be used in a server.");
            return false;
        }

        if (ctx.Member is null || !ctx.Member.Permissions.HasPermission(DiscordPermission.ManageGuild))
        {
            await ctx.RespondAsync("❌ You need the **Manage Server** permission to use this command.");
            return false;
        }

        return true;
    }

    private static async Task<GuildConfig> GetOrCreateConfigAsync(AppDbContext db, ulong guildId)
    {
        var config = await db.GuildConfigs.FirstOrDefaultAsync(g => g.GuildId == guildId);
        if (config == null)
        {
            config = new GuildConfig { GuildId = guildId };
            db.GuildConfigs.Add(config);
        }
        return config;
    }
}
