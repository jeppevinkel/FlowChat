using System.Diagnostics.CodeAnalysis;
using System.Text;
using Anthropic.SDK.Common;
using Discord;
using Discord.WebSocket;
using FlowChat.Services.Implementations;
using Microsoft.Extensions.Logging;

namespace FlowChat.Tools;

public class VoiceChannelTools
{
    private SocketMessage _message;
    private readonly GuildContextManager _contextManager;
    private readonly ILogger<VoiceChannelTools> _logger;

    public VoiceChannelTools(SocketMessage message, GuildContextManager contextManager,
        ILogger<VoiceChannelTools> logger)
    {
        _message = message;
        _contextManager = contextManager;
        _logger = logger;
    }

    private bool GetGuildContext(
        [NotNullWhen(true)] out GuildContext? context,
        [NotNullWhen(false)] out string? errorMessage)
    {
        context = null;
        errorMessage = null;

        if (_message.Author is not IGuildUser guildUser)
        {
            errorMessage = "The user is not in a guild";
            return false;
        }

        context = _contextManager.GetOrCreateContext(guildUser.Guild.Id);
        return true;
    }
    
    private bool TryGetVoiceService(
        [NotNullWhen(true)] out VoiceService? voiceService, 
        [NotNullWhen(false)] out string? errorMessage,
        bool requireConnected = false)
    {
        voiceService = null;
        
        if (!GetGuildContext(out var context, out errorMessage))
        {
            return false;
        }
        
        voiceService = context.GetVoiceService();
        
        if (requireConnected && !voiceService.IsConnected)
        {
            errorMessage = "Not connected to a voice channel";
            voiceService = null;
            return false;
        }
        
        return true;
    }

    [Function(
        "Join the voice chat of the user. Usually only use this if explicitly requested or if the user wants you to play music and you aren't already in another voice chat")]
    public async Task<string> JoinVoiceChannel()
    {
        try
        {
            if (!TryGetVoiceService(out VoiceService? voiceService, out var errorMessage))
            {
                return errorMessage;
            }

            if (voiceService.IsConnected)
            {
                return $"Already connected to \"{voiceService.ConnectedChannel}\"";
            }
            
            if (_message.Author is not IGuildUser guildUser || guildUser.VoiceChannel is null)
            {
                return "You must be in a voice channel for me to join";
            }

            _logger.LogInformation(
                "User {User} requested bot to join voice channel {Channel} in guild {Guild}",
                guildUser.Username,
                guildUser.VoiceChannel.Name,
                guildUser.Guild.Name);

            await voiceService.ConnectAsync(guildUser.VoiceChannel.Id);
            return $"Joined voice channel: {voiceService.ConnectedChannel}";
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error joining voice channel");
            return $"Error connecting to channel: {e.Message}";
        }
    }

    [Function(
        "Leave the currently connected voice chat")]
    public async Task<string> LeaveVoiceChannel()
    {
        try
        {
            if (!TryGetVoiceService(out VoiceService? voiceService, out var errorMessage, requireConnected: true))
            {
                return errorMessage;
            }

            var voiceChannelName = voiceService.ConnectedChannel;
            await voiceService.DisconnectAsync();

            _logger.LogInformation("Left voice channel {Channel}", voiceChannelName);
            return $"Left voice channel: {voiceChannelName}";
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error leaving voice channel");
            return $"Error disconnecting from channel: {e.Message}";
        }
    }

    [Function(
        "Play music in the currently connected voice channel")]
    public async Task<string> PlayMusic(
        [FunctionParameter("The query to search on YouTube", true)] string searchQuery)
    {
        try
        {
            if (!TryGetVoiceService(out VoiceService? voiceService, out var errorMessage, requireConnected: true))
            {
                return errorMessage;
            }

            var result = await voiceService.SearchAndQueueMusicAsync(searchQuery);
            
            _logger.LogInformation("Queued music: {Title} at position {Position}", 
                result.Title, result.QueuePosition);

            return result.QueuePosition == 0
                ? $"Now playing: {result.Title}"
                : $"Added to queue (position {result.QueuePosition}): {result.Title}";
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error playing music");
            return $"Error playing music: {e.Message}";
        }
    }

    [Function(
        "Skip the currently playing music in the currently connected voice channel")]
    public Task<string> SkipMusic()
    {
        try
        {
            if (!TryGetVoiceService(out VoiceService? voiceService, out var errorMessage, requireConnected: true))
            {
                return Task.FromResult(errorMessage);
            }

            var currentTrack = voiceService.GetCurrentTrack()?.Title;

            if (!voiceService.SkipCurrentSong())
            {
                return Task.FromResult("No music to skip.");
            }

            _logger.LogInformation("Skipped track: {Track}", currentTrack);
            return Task.FromResult($"Skipped {currentTrack}");
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error skipping music");
            return Task.FromResult($"Error skipping music: {e.Message}");
        }
    }

    [Function(
        "Returns the current queue of music in the currently connected voice channel (it is not displayed to the user by default)")]
    public Task<string> GetQueue()
    {
        try
        {
            if (!TryGetVoiceService(out VoiceService? voiceService, out var errorMessage, requireConnected: true))
            {
                return Task.FromResult(errorMessage);
            }

            var currentTrack = voiceService.GetCurrentTrack();
            var queuedTracks = voiceService.GetQueuedTracks();

            StringBuilder sb = new StringBuilder();

            if (currentTrack != null)
            {
                sb.AppendLine("Now playing:");
                sb.AppendLine($"Title: {currentTrack.Title}");
                sb.AppendLine($"Duration: {currentTrack.Duration?.ToString(@"mm\:ss") ?? "Unknown duration"}");
            }
            else
            {
                sb.AppendLine("No music is currently playing.");
            }

            if (queuedTracks.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Queue:");
                for (int i = 0; i < queuedTracks.Count; i++)
                {
                    var track = queuedTracks[i];
                    sb.AppendLine($"- Title: {track.Title}");
                    sb.AppendLine($"  Duration: {track.Duration?.ToString(@"mm\:ss") ?? "Unknown duration"}");
                    if (i < queuedTracks.Count - 1)
                    {
                        sb.AppendLine();
                    }
                }
            }
            else
            {
                sb.AppendLine("No music is queued.");
            }

            return Task.FromResult(sb.ToString());
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error getting queue");
            return Task.FromResult($"Error getting queue: {e.Message}");
        }
    }

    [Function("Say something in the currently connected voice channel")]
    public async Task<string> SayInVoiceChannel(
        [FunctionParameter("The message to say", true)] string message)
    {
        try
        {
            if (!TryGetVoiceService(out VoiceService? voiceService, out var errorMessage, requireConnected: true))
            {
                return errorMessage;
            }

            await voiceService.PlayTextToSpeechAsync(message);

            _logger.LogInformation("Speaking TTS in voice channel");
            return "Speaking...";
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error speaking in voice channel");
            return $"Error speaking in voice channel: {e.Message}";
        }
    }

    [Function("Set the music volume to a value between 0.0 and 2.0 (default value is 0.2)")]
    public Task<string> SetMusicVolume(
        [FunctionParameter("The volume to set", true)] float volume)
    {
        try
        {
            if (!TryGetVoiceService(out VoiceService? voiceService, out var errorMessage, requireConnected: true))
            {
                return Task.FromResult(errorMessage);
            }

            volume = voiceService.SetMusicVolume(volume);

            return Task.FromResult($"The volume has been set to {volume}");
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error setting the volume");
            return Task.FromResult($"Error setting the volume: {e.Message}");
        }
    }
}