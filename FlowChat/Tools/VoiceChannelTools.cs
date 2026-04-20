using System.Diagnostics.CodeAnalysis;
using Anthropic.SDK.Common;
using Discord;
using Discord.WebSocket;
using FlowChat.Helpers;
using FlowChat.Services.Implementations;
using Microsoft.Extensions.Logging;

namespace FlowChat.Tools;

public class VoiceChannelTools
{
    private readonly SocketMessage _message;
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
                return ToolResult.Failure(errorMessage);
            }

            if (voiceService.IsConnected)
            {
                return ToolResult.Failure($"Already connected to \"{voiceService.ConnectedChannel}\"");
            }
            
            if (_message.Author is not IGuildUser guildUser || guildUser.VoiceChannel is null)
            {
                return ToolResult.Failure("You must be in a voice channel for me to join");
            }

            _logger.LogInformation(
                "User {User} requested bot to join voice channel {Channel} in guild {Guild}",
                guildUser.Username,
                guildUser.VoiceChannel.Name,
                guildUser.Guild.Name);

            await voiceService.ConnectAsync(guildUser.VoiceChannel.Id);
            return ToolResult.Success($"Joined voice channel: {voiceService.ConnectedChannel}");
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error joining voice channel");
            return ToolResult.Failure($"Error connecting to channel: {e.Message}");
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
                return ToolResult.Failure(errorMessage);
            }

            var voiceChannelName = voiceService.ConnectedChannel;
            await voiceService.DisconnectAsync();

            _logger.LogInformation("Left voice channel {Channel}", voiceChannelName);
            return ToolResult.Success($"Left voice channel: {voiceChannelName}");
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error leaving voice channel");
            return ToolResult.Failure($"Error disconnecting from channel: {e.Message}");
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
                return ToolResult.Failure(errorMessage);
            }

            voiceService.SetMusicInteractionChannel(_message.Channel as SocketTextChannel);
            var result = await voiceService.SearchAndQueueMusicAsync(searchQuery);

            _logger.LogInformation("Queued music: {Title} at position {Position}",
                result.Title, result.QueuePosition);

            return result.QueuePosition == 0
                ? ToolResult.Success($"Now playing: {result.Title}")
                : ToolResult.Success($"Added to queue (position {result.QueuePosition}): {result.Title}");
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error playing music");
            return ToolResult.Failure($"Error playing music: {e.Message}");
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
                return Task.FromResult(ToolResult.Failure(errorMessage));
            }

            voiceService.SetMusicInteractionChannel(_message.Channel as SocketTextChannel);
            var currentTrack = voiceService.GetCurrentTrack()?.Title;

            if (!voiceService.SkipCurrentSong())
            {
                return Task.FromResult(ToolResult.Failure("No music to skip."));
            }

            _logger.LogInformation("Skipped track: {Track}", currentTrack);
            return Task.FromResult(ToolResult.Success($"Skipped {currentTrack}"));
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error skipping music");
            return Task.FromResult(ToolResult.Failure($"Error skipping music: {e.Message}"));
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
                return Task.FromResult(ToolResult.Failure(errorMessage));
            }

            var currentTrack = voiceService.GetCurrentTrack();
            var queuedTracks = voiceService.GetQueuedTracks();
            var currentProgress = voiceService.GetCurrentTrackProgress();
            var remainingQueueTime = voiceService.GetRemainingQueueTime();

            return Task.FromResult(ToolResult.Success(new
            {
                CurrentTrack = currentTrack,
                CurrentProgress = currentProgress,
                QueuedTracks = queuedTracks,
                TotalRemainingQueueTime = remainingQueueTime
            }));
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error getting queue");
            return Task.FromResult(ToolResult.Failure($"Error getting queue: {e.Message}"));
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
                return ToolResult.Failure(errorMessage);
            }

            await voiceService.PlayTextToSpeechAsync(message);

            _logger.LogInformation("Speaking TTS in voice channel");
            return ToolResult.Success("Speaking...");
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error speaking in voice channel");
            return ToolResult.Failure($"Error speaking in voice channel: {e.Message}");
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
                return Task.FromResult(ToolResult.Failure(errorMessage));
            }

            volume = voiceService.SetMusicVolume(volume);

            return Task.FromResult(ToolResult.Success($"The volume has been set to {volume}"));
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error setting the volume");
            return Task.FromResult(ToolResult.Failure($"Error setting the volume: {e.Message}"));
        }
    }
}