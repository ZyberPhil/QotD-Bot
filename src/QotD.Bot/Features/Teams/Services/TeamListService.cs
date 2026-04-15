using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using QotD.Bot.Data;
using QotD.Bot.UI;
using System.Text;

namespace QotD.Bot.Features.Teams.Services;

public sealed class TeamListService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<TeamListService> _logger;

    public TeamListService(IServiceProvider serviceProvider, ILogger<TeamListService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task RefreshTeamListAsync(DiscordClient client, ulong guildId)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var config = await db.TeamListConfigs.FirstOrDefaultAsync(c => c.GuildId == guildId);
            if (config == null || config.ChannelId == null || config.TrackedRoles.Length == 0) return;

            if (!client.Guilds.TryGetValue(config.GuildId, out var guild)) return;
            if (!guild.Channels.TryGetValue(config.ChannelId.Value, out var channel)) return;

            var allMembers = new List<DiscordMember>();
            await foreach (var member in guild.GetAllMembersAsync())
            {
                allMembers.Add(member);
            }

            var embed = new DiscordEmbedBuilder()
                .WithTitle(config.CustomTitle ?? "📋 Team List")
                .WithColor(SectorUI.SectorPrimary)
                .WithTimestamp(DateTimeOffset.UtcNow);

            if (!string.IsNullOrWhiteSpace(config.CustomFooter))
            {
                embed.WithFooter(config.CustomFooter);
            }

            var sb = new StringBuilder();
            
            // Build default or custom template format
            var trackedRoles = config.TrackedRoles
                .Select(id => guild.Roles.GetValueOrDefault(id))
                .Where(r => r != null)
                .OrderByDescending(r => r?.Position ?? 0)
                .ToList();

            foreach (var role in trackedRoles)
            {
                var membersWithRole = allMembers.Where(m => m.Roles.Any(r => r.Id == role.Id)).ToList();
                string membersList = membersWithRole.Count > 0 
                    ? string.Join(", ", membersWithRole.Select(m => $"<@{m.Id}>"))
                    : "Nobody";

                    if (!string.IsNullOrWhiteSpace(config.CustomTemplate))
                    {
                        var block = BotPromptTokens.ApplyTeamTemplate(
                            config.CustomTemplate,
                            role.Name,
                            role.Mention,
                            membersWithRole.Count.ToString(),
                            membersList);
                        sb.AppendLine(block);
                    }
                    else
                    {
                        sb.AppendLine($"**{role.Name}** ({membersWithRole.Count})");
                        sb.AppendLine($"👥 {membersList}\n");
                    }
                }

            embed.WithDescription(sb.ToString());

            // Check if we need to edit or create message
            DiscordMessage? targetMessage = null;
            if (config.MessageId.HasValue && config.MessageId.Value > 0)
            {
                try
                {
                    targetMessage = await channel.GetMessageAsync(config.MessageId.Value);
                }
                catch { /* Message deleted or inaccessible */ }
            }

            if (targetMessage != null)
            {
                await targetMessage.ModifyAsync(embed.Build());
            }
            else
            {
                var newMessage = await channel.SendMessageAsync(embed.Build());
                config.MessageId = newMessage.Id;
                await db.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh dynamic team list in guild {GuildId}", guildId);
        }
    }
}
