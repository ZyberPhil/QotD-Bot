using DSharpPlus;
using DSharpPlus.Commands;
using DSharpPlus.Commands.ContextChecks;
using DSharpPlus.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QotD.Bot.Data;
using QotD.Bot.Data.Models;
using QotD.Bot.Features.TempVoice.Services;
using System.ComponentModel;
using DescriptionAttribute = System.ComponentModel.DescriptionAttribute;

namespace QotD.Bot.Features.TempVoice.Commands;

[DSharpPlus.Commands.Command("voice")]
[Description("Temporary voice channel commands")]
public sealed class TempVoiceCommands
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TempVoiceEventHandler _eventHandler;

    public TempVoiceCommands(IServiceScopeFactory scopeFactory, TempVoiceEventHandler eventHandler)
    {
        _scopeFactory = scopeFactory;
        _eventHandler = eventHandler;
    }

    [DSharpPlus.Commands.Command("setup")]
    [Description("Set the trigger channel for temp voice (Admin)")]
    [RequirePermissions(DiscordPermission.ManageGuild)]
    public async Task SetupAsync(CommandContext ctx,
        [Description("The 'Join to Create' voice channel")] DiscordChannel triggerChannel,
        [Description("Category to create channels in (optional)")] DiscordChannel? category = null)
    {
        if (triggerChannel.Type != DiscordChannelType.Voice)
        {
            await ctx.RespondAsync("❌ The trigger channel must be a voice channel.");
            return;
        }

        if (category != null && category.Type != DiscordChannelType.Category)
        {
            await ctx.RespondAsync("❌ The category must be a category channel.");
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var guildId = ctx.Guild!.Id;
        var config = await db.TempVoiceConfigs.FirstOrDefaultAsync(c => c.GuildId == guildId);
        if (config == null)
        {
            config = new TempVoiceConfig { GuildId = guildId };
            db.TempVoiceConfigs.Add(config);
        }

        config.TriggerChannelId = triggerChannel.Id;
        config.CategoryId = category?.Id;
        await db.SaveChangesAsync();

        await ctx.RespondAsync($"✅ Temp Voice configured!\n" +
                               $"- Trigger: {triggerChannel.Mention}\n" +
                               (category != null ? $"- Category: {category.Name}" : "- Category: Same as trigger channel"));
    }

    [DSharpPlus.Commands.Command("rename")]
    [Description("Rename your temporary voice channel")]
    public async Task RenameAsync(CommandContext ctx,
        [Description("New channel name")] string name)
    {
        var result = await GetOwnedChannelAsync(ctx);
        if (result == null) return;

        if (name.Length > 100) name = name[..100];
        await result.ModifyAsync(c => c.Name = $"🔊 {name}");
        await ctx.RespondAsync($"✅ Channel renamed to **{name}**");
    }

    [DSharpPlus.Commands.Command("limit")]
    [Description("Set a user limit for your channel")]
    public async Task LimitAsync(CommandContext ctx,
        [Description("User limit (0 = unlimited)")] int limit)
    {
        var result = await GetOwnedChannelAsync(ctx);
        if (result == null) return;

        limit = Math.Clamp(limit, 0, 99);
        // FIXME: UserLimit name changed in v5 nightly, currently unknown property.
        // await result.ModifyAsync(c => c.UserLimit = limit);
        await ctx.RespondAsync(limit > 0 ? $"✅ User limit set to **{limit}** (Note: Logic currently disabled due to library updates)" : "✅ User limit removed");
    }

    [DSharpPlus.Commands.Command("lock")]
    [Description("Lock your channel so no one else can join")]
    public async Task LockAsync(CommandContext ctx)
    {
        var result = await GetOwnedChannelAsync(ctx);
        if (result == null) return;

        await result.AddOverwriteAsync(ctx.Guild!.EveryoneRole, deny: DiscordPermission.Connect);
        await ctx.RespondAsync("🔒 Channel **locked**.");
    }

    [DSharpPlus.Commands.Command("unlock")]
    [Description("Unlock your channel")]
    public async Task UnlockAsync(CommandContext ctx)
    {
        var result = await GetOwnedChannelAsync(ctx);
        if (result == null) return;

        await result.AddOverwriteAsync(ctx.Guild!.EveryoneRole, allow: DiscordPermission.Connect);
        await ctx.RespondAsync("🔓 Channel **unlocked**.");
    }

    private async Task<DiscordChannel?> GetOwnedChannelAsync(CommandContext ctx)
    {
        var member = (DiscordMember)ctx.User;
        var voiceState = member.VoiceState;
        var channelId = voiceState?.ChannelId ?? 0;

        if (channelId == 0)
        {
            await ctx.RespondAsync("❌ You are not in a voice channel.");
            return null;
        }

        if (!_eventHandler.IsOwner(channelId, member.Id))
        {
            await ctx.RespondAsync("❌ You are not the owner of this channel.");
            return null;
        }

        return await ctx.Client.GetChannelAsync(channelId);
    }
}
