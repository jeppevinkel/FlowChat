using System.Collections.Concurrent;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FlowChat.Services.Implementations;

public class GuildContextManager : IDisposable
{
    private readonly ILogger<GuildContextManager> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly DiscordSocketClient _discordClient;
    private readonly ConcurrentDictionary<ulong, GuildContext> _guildContexts = new();

    public GuildContextManager(
        ILogger<GuildContextManager> logger,
        IServiceProvider serviceProvider,
        DiscordSocketClient discordClient)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _discordClient = discordClient;
    }

    /// <summary>
    /// Gets or creates a context for the specified guild.
    /// </summary>
    public GuildContext GetOrCreateContext(ulong guildId)
    {
        return _guildContexts.GetOrAdd(guildId, id =>
        {
            _logger.LogInformation("Creating new context for guild {GuildId}", id);
            var context = ActivatorUtilities.CreateInstance<GuildContext>(_serviceProvider, id);
            return context;
        });
    }
    
    /// <summary>
    /// Gets context for a guild if it exists.
    /// </summary>
    public GuildContext? GetContext(ulong guildId)
    {
        _guildContexts.TryGetValue(guildId, out var context);
        return context;
    }
    
    /// <summary>
    /// Removes and disposes a guild context (cleanup when bot leaves guild or on disconnect).
    /// </summary>
    public async Task RemoveContextAsync(ulong guildId)
    {
        if (_guildContexts.TryRemove(guildId, out var context))
        {
            _logger.LogInformation("Removing context for guild {GuildId}", guildId);
            await context.DisposeAsync();
        }
    }
    
    public IReadOnlyCollection<GuildContext> GetAllContexts()
    {
        return _guildContexts.Values.ToList();
    }

    public void Dispose()
    {
        foreach (var context in _guildContexts.Values)
        {
            context.Dispose();
        }
        _guildContexts.Clear();
    }
}