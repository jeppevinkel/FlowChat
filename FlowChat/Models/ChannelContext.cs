using Anthropic.SDK.Messaging;
using Discord.WebSocket;
using FlowChat.Services.Implementations;
using FlowChat.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FlowChat.Models;

public class ChannelContext
{
    public readonly MemoryManager MemoryManager;
    public readonly VoiceChannelTools VoiceChannelTools;
    public readonly List<Message> Messages = [];

    public ChannelContext(IServiceProvider services, SocketMessage message)
    {
        MemoryManager = new MemoryManager(message.Channel.Id);
        IServiceScope scope = services.CreateScope();
        VoiceChannelTools = new VoiceChannelTools(message, scope.ServiceProvider.GetRequiredService<GuildContextManager>(), scope.ServiceProvider.GetRequiredService<ILogger<VoiceChannelTools>>());
    }
}