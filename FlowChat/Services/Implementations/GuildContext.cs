using System.Collections.Concurrent;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FlowChat.Services.Implementations;

public class GuildContext : IAsyncDisposable, IDisposable
{
    private readonly ILogger<GuildContext> _logger;
    private readonly DiscordSocketClient _discordClient;
    private readonly IServiceProvider _serviceProvider;
    
    public ulong GuildId { get; }
    
    // Feature-specific services
    private VoiceService? _voiceService;
    
    // Future features can be added here
    // private ModerationService? _moderationService;
    // private CustomCommandsService? _customCommandsService;
    
    // Guild-specific memory/state
    public ConcurrentDictionary<string, object> State { get; } = new();

    public GuildContext(
        ulong guildId,
        ILogger<GuildContext> logger,
        DiscordSocketClient discordClient,
        IServiceProvider serviceProvider)
    {
        GuildId = guildId;
        _logger = logger;
        _discordClient = discordClient;
        _serviceProvider = serviceProvider;
    }
    
    /// <summary>
    /// Gets or initializes the voice service for this guild.
    /// </summary>
    public VoiceService GetVoiceService()
    {
        if (_voiceService == null)
        {
            _voiceService = ActivatorUtilities.CreateInstance<VoiceService>(
                _serviceProvider, 
                GuildId);
        }
        return _voiceService;
    }
    
    public SocketGuild? GetGuild()
    {
        return _discordClient.GetGuild(GuildId);
    }
    
    public async ValueTask DisposeAsync()
    {
        if (_voiceService != null)
        {
            await _voiceService.DisposeAsync();
        }
    }
    
    public void Dispose()
    {
        _voiceService?.Dispose();
    }
}