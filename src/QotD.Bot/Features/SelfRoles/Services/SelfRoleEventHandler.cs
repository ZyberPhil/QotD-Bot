using DSharpPlus;
using DSharpPlus.EventArgs;

namespace QotD.Bot.Features.SelfRoles.Services;

public sealed class SelfRoleEventHandler :
    IEventHandler<MessageReactionAddedEventArgs>,
    IEventHandler<MessageReactionRemovedEventArgs>,
    IEventHandler<ComponentInteractionCreatedEventArgs>
{
    private readonly SelfRoleService _service;

    public SelfRoleEventHandler(SelfRoleService service)
    {
        _service = service;
    }

    public async Task HandleEventAsync(DiscordClient client, MessageReactionAddedEventArgs e)
    {
        await _service.HandleReactionAddedAsync(client, e);
    }

    public async Task HandleEventAsync(DiscordClient client, MessageReactionRemovedEventArgs e)
    {
        await _service.HandleReactionRemovedAsync(client, e);
    }

    public async Task HandleEventAsync(DiscordClient client, ComponentInteractionCreatedEventArgs e)
    {
        await _service.HandleModerationButtonAsync(client, e);
    }
}