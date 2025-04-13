using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FlowChat;

public class DiscordClient
{
    private readonly ILogger<DiscordClient> _logger;
    private readonly IConfiguration _config;
    public readonly DiscordSocketClient Client;
    
    public DiscordClient(ILogger<DiscordClient> logger, IConfiguration config)
    {
        _logger = logger;
        _config = config;
        Client = new DiscordSocketClient();
        Client.Log += Log;
    }
    
    private Task Log(LogMessage msg)
    {
        switch (msg.Severity)
        {
            case LogSeverity.Info:
                _logger.LogInformation("{Message}", msg);
                break;
            case LogSeverity.Debug:
                _logger.LogDebug("{Message}", msg);
                break;
            case LogSeverity.Critical:
                _logger.LogCritical("{Message}", msg);
                break;
            case LogSeverity.Error:
                _logger.LogError("{Message}", msg);
                break;
            case LogSeverity.Warning:
                _logger.LogWarning("{Message}", msg);
                break;
            case LogSeverity.Verbose:
                _logger.LogTrace("{Message}", msg);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(msg));
        }

        return Task.CompletedTask;
    }
}