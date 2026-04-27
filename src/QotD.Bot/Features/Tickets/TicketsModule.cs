using DSharpPlus.Commands;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QotD.Bot.Core;
using QotD.Bot.Features.Tickets.Commands;
using QotD.Bot.Features.Tickets.Services;

namespace QotD.Bot.Features.Tickets;

public sealed class TicketsModule : IBotModule
{
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<TicketService>();
    }

    public void ConfigureDiscordServices(IServiceCollection services, IServiceProvider hostProvider)
    {
        services.AddScoped<TicketService>();
    }

    public void ConfigureCommands(CommandsExtension commands)
    {
        commands.AddCommands<TicketSetupCommand>();
        commands.AddCommands<TicketCommand>();
    }
}
