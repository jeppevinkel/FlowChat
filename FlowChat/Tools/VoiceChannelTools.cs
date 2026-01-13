using System.Text;
using Anthropic.SDK.Common;
using Discord;
using Discord.WebSocket;

namespace FlowChat.Tools;

public class VoiceChannelTools
{
    private SocketMessage _message;
    private VoiceChannelContext _voiceChannelContext;
    public VoiceChannelTools(SocketMessage message, VoiceChannelContext voiceChannelContext)
    {
        _message = message;
        _voiceChannelContext = voiceChannelContext;
    }

    [Function(
        "Join the voice chat of the user. Usually only use this if explicitly requested")]
    public async Task<string> JoinVoice()
    {
        if (_voiceChannelContext.ConnectedChannel() is { } connectedChannel)
        {
            return $"Already connected to \"{connectedChannel}\".";
        }

        SocketUser? author = _message.Author;

        if (author is not IGuildUser guildUser)
        {
            return "The user is not in a guild.";
        }
        
        if (guildUser.VoiceChannel is null)
        {
            return "The user is not in a voice channel.";
        }

        try
        {
            await _voiceChannelContext.ConnectVoiceAsync(guildUser.VoiceChannel);
        }
        catch (Exception e)
        {
            return $"Error connecting to channel: {e}";
        }
        return $"Connected to {guildUser.VoiceChannel.Name}.";
    }

    [Function(
        "Leave the currently connected voice chat")]
    public async Task<string> LeaveVoice()
    {
        if (_voiceChannelContext.ConnectedChannel() is not { } connectedChannel)
        {
            return "Not currently in any voice chat.";
        }

        await _voiceChannelContext.DisconnectVoiceAsync();
        return "Disconnected from the voice chat.";
    }

    [Function(
        "Play music in the currently connected voice channel")]
    public async Task<string> PlayMusic([FunctionParameter("The query to search on YouTube", true)] string searchQuery)
    {
        if (_voiceChannelContext.ConnectedChannel() is null)
        {
            return "Must be in a voice channel to play music.";
        }

        try
        {
            VoiceChannelContext.SongSearchResult songSearchResult = await _voiceChannelContext.SearchMusic(searchQuery);

            if (songSearchResult.QueuePosition == 0)
            {
                return $"Now playing: {songSearchResult.Title}";
            }

            return $"{songSearchResult.Title} is now number {songSearchResult.QueuePosition} in queue after the current song.";
        }
        catch (Exception e)
        {
            return $"Error playing music: {e}";
        }
    }

    [Function(
        "Skip the currently playing music in the currently connected voice channel")]
    public async Task<string> SkipMusic()
    {
        if (_voiceChannelContext.ConnectedChannel() is null)
        {
            return "Must be in a voice channel to skip music.";
        }

        try
        {
            var result = _voiceChannelContext.SkipCurrentSong();

            if (result)
            {
                return "Skipped current song.";
            }

            return "No song to skip.";
        }
        catch (Exception e)
        {
            return $"Error skipping music: {e}";
        }
    }
    
    [Function(
        "Returns the current queue of music in the currently connected voice channel")]
    public async Task<string> GetQueue()
    {
        if (_voiceChannelContext.ConnectedChannel() is null)
        {
            return "Must be in a voice channel to get the queue.";
        }

        try
        {
            var currentTrack = _voiceChannelContext.GetCurrentTrack();
            var queuedTracks = _voiceChannelContext.GetQueuedTracks();
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
            
            Console.WriteLine(sb.ToString());
            
            return sb.ToString();
        }
        catch (Exception e)
        {
            return $"Error getting queue: {e}";
        }
    }
    
    [Function("Say something in the currently connected voice channel")]
    public async Task<string> SayInVoiceChannel([FunctionParameter("The message to say", true)] string message)
    {
        if (_voiceChannelContext.ConnectedChannel() is null)
        {
            return "Must be in a voice channel to say something.";
        }

        try
        {
            await _voiceChannelContext.PlayTextToSpeech(message);
            
            return $"Said '{message}' in the voice channel.";
        } catch (Exception e)
        {
            return $"Error saying in voice channel: {e}";
        }
    }
}