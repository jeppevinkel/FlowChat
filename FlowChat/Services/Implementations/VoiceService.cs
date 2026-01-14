using System.Collections.Concurrent;
using Discord.Audio;
using Discord.WebSocket;
using FlowChat.Models;
using Microsoft.Extensions.Logging;
using YoutubeExplode;
using YoutubeExplode.Videos.Streams;

namespace FlowChat.Services.Implementations;

public class VoiceService : IAsyncDisposable, IDisposable
{
    private readonly ILogger<VoiceService> _logger;
    private readonly DiscordSocketClient _discordClient;
    private readonly ElevenLabsService _elevenLabsService;
    private readonly ulong _guildId;
    
    private SocketGuild? _guild;
    private SocketVoiceChannel? _voiceChannel;
    private IAudioClient? _voiceConnection;
    private AudioMixingService? _mixer;
    private CancellationTokenSource? _mixerCts;
    
    private readonly ConcurrentQueue<QueuedTrack> _trackQueue = new();
    private readonly SemaphoreSlim _queueSignal = new(0);
    private QueuedTrack? _currentTrack;
    private Task? _queueProcessorTask;
    
    private const string MusicSourceId = "music";
    private const string TtsSourceId = "tts";
    
    private float _musicVolume = 0.2f;
    private float _ttsVolume = 1.0f;
    
    public bool IsPlaying => _mixer?.ActiveSourceCount > 0;
    public bool IsMusicPlaying => _mixer?.HasSource(MusicSourceId) ?? false;
    public bool IsTtsPlaying => _mixer?.HasSource(TtsSourceId) ?? false;
    public bool IsConnected => _voiceChannel is not null && _mixer is not null;
    public string? ConnectedChannel => _voiceChannel?.Name;
    
    public VoiceService(
        ulong guildId,
        ILogger<VoiceService> logger,
        DiscordSocketClient discordClient,
        ElevenLabsService elevenLabsService)
    {
        _guildId = guildId;
        _logger = logger;
        _discordClient = discordClient;
        _elevenLabsService = elevenLabsService;
    }
    
    public async Task ConnectAsync(ulong channelId)
    {
        if (_voiceChannel is not null)
        {
            await DisconnectAsync();
        }

        try
        {
            _guild = _discordClient.GetGuild(_guildId);
            _voiceChannel = _guild.GetVoiceChannel(channelId);
            _voiceConnection = await _voiceChannel.ConnectAsync();
            
            await StartMixerAsync();
            StartQueueProcessor();
            
            _logger.LogInformation("[Guild: {GuildId}] Connected to voice channel: {ChannelName}", 
                _guildId, _voiceChannel.Name);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[Guild: {GuildId}] Error connecting to voice", _guildId);
            throw;
        }
    }
    
    private async Task StartMixerAsync()
    {
        if (_voiceConnection == null)
            throw new InvalidOperationException("Voice connection not established");

        _mixer = new AudioMixingService();
        _mixerCts = new CancellationTokenSource();
        
        var audioOutStream = _voiceConnection.CreatePCMStream(AudioApplication.Mixed);
        
        // Start the mixing loop in the background
        _ = Task.Run(async () =>
        {
            try
            {
                await _mixer.StartMixingAsync(audioOutStream, _mixerCts.Token);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("[Guild: {GuildId}] Audio mixer stopped", _guildId);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "[Guild: {GuildId}] Error in audio mixer", _guildId);
            }
            finally
            {
                await audioOutStream.DisposeAsync();
            }
        }, _mixerCts.Token);
        
        // Give the mixer a moment to start
        await Task.Delay(100);
    }
    
    private void StartQueueProcessor()
    {
        _queueProcessorTask = Task.Run(async () =>
        {
            while (!_mixerCts?.Token.IsCancellationRequested ?? false)
            {
                try
                {
                    // Wait for a track to be queued
                    await _queueSignal.WaitAsync(_mixerCts.Token);
                    
                    // Dequeue and play the track
                    if (_trackQueue.TryDequeue(out var track))
                    {
                        await PlayMusicAsync(track, _mixerCts.Token);
                    }
                }
                catch (OperationCanceledException)
                {
                    // Expected on shutdown
                    break;
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "[Guild: {GuildId}] Error processing music queue", _guildId);
                }
            }
        });
    }
    
    /// <summary>
    /// Disconnect from the current channel if connected to any.
    /// </summary>
    public async Task DisconnectAsync()
    {
        if (_voiceChannel is not null)
        {
            // Stop mixer first
            _mixerCts?.Cancel();
            _mixer?.Dispose();
            _mixer = null;
            
            if (_queueProcessorTask != null)
            {
                await _queueProcessorTask;
                _queueProcessorTask = null;
            }
            
            _mixerCts?.Dispose();
            _mixerCts = null;
            
            // Disconnect from voice
            await _voiceChannel.DisconnectAsync();
            _voiceChannel = null;
            _guild = null;
            _voiceConnection = null;
            
            ClearQueue();
            
            _logger.LogInformation("[Guild: {GuildId}] Disconnected from voice", _guildId);
        }
    }
    
    /// <summary>
    /// Searches for a video and plays the audio.
    /// </summary>
    /// <param name="searchQuery"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<SongSearchResult> SearchAndQueueMusicAsync(
        string searchQuery, 
        CancellationToken cancellationToken = default)
    {
        if (!IsConnected)
            throw new InvalidOperationException("Not connected to a voice channel");
        
        _logger.LogInformation("[Guild: {GuildId}] Searching for: {Query}", _guildId, searchQuery);
        
        var youtube = new YoutubeClient();
        var result = await youtube.Search.GetVideosAsync(searchQuery, cancellationToken)
            .FirstAsync(cancellationToken);
        
        // Create track metadata
        var track = new QueuedTrack
        {
            Title = result.Title,
            Url = result.Url,
            Duration = result.Duration
        };
        
        // Add to metadata queue
        _trackQueue.Enqueue(track);
        _queueSignal.Release();
        
        return new SongSearchResult
        {
            Title = result.Title,
            QueuePosition = _trackQueue.Count - 1 + (IsMusicPlaying ? 1 : 0)
        };
    }
    
    private async ValueTask PlayMusicAsync(QueuedTrack track, CancellationToken cancellationToken)
    {
        if (_mixer is null) return;
        
        _currentTrack = track;
        try
        {
            _logger.LogInformation("[Guild: {GuildId}] Playing: {Title}", _guildId, track.Title);
            
            var youtube = new YoutubeClient();
            var streamManifest = await youtube.Videos.Streams.GetManifestAsync(
                track.Url, cancellationToken);
            var streamInfo = streamManifest.GetAudioOnlyStreams().GetWithHighestBitrate();
            var audioStream = await youtube.Videos.Streams.GetAsync(streamInfo, cancellationToken);
            
            // Add to mixer (will be converted to PCM inside mixer)
            await _mixer.AddSourceAsync(MusicSourceId, audioStream, volume: _musicVolume, cancellationToken);
            await _mixer.WaitForSourceCompletionAsync(MusicSourceId);
            
            _logger.LogInformation("[Guild: {GuildId}] Finished: {Title}", _guildId, track.Title);
        }
        catch (OperationCanceledException)
        {
            _mixer?.RemoveSource(MusicSourceId);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[Guild: {GuildId}] Error playing: {Title}", _guildId, track.Title);
            _mixer?.RemoveSource(MusicSourceId);
        }
        finally
        {
            _currentTrack = null;
        }
    }
    
    public async Task PlayTextToSpeechAsync(string text, CancellationToken cancellationToken = default)
    {
        if (_mixer == null)
            throw new InvalidOperationException("Not connected to a voice channel");

        try
        {
            _logger.LogInformation("[Guild: {GuildId}] Generating TTS", _guildId);
            
            // Generate TTS
            var voiceClip = await _elevenLabsService.GenerateTextToSpeechAsync(text, cancellationToken);
            // Create stream from bytes
            var inputStream = new MemoryStream(voiceClip.ClipData.ToArray());
            
            // Add to mixer with high volume (plays over music)
            await _mixer.AddSourceAsync(TtsSourceId, inputStream, volume: _ttsVolume, cancellationToken);
            await _mixer.WaitForSourceCompletionAsync(TtsSourceId);
        }
        catch (OperationCanceledException)
        {
            _mixer?.RemoveSource(TtsSourceId);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[Guild: {GuildId}] Error playing TTS", _guildId);
            _mixer?.RemoveSource(TtsSourceId);
            throw;
        }
    }

    public float SetMusicVolume(float volume)
    {
        _musicVolume = Math.Clamp(volume, 0f, 2f);
        _mixer?.SetVolume(MusicSourceId, _musicVolume);
        
        return _musicVolume;
    }
    
    public void SetTtsVolume(float volume)
    {
        _ttsVolume = Math.Clamp(volume, 0f, 2f);
        _mixer?.SetVolume(TtsSourceId, _ttsVolume);
    }
    
    public bool SkipCurrentSong()
    {
        return _mixer?.RemoveSource(MusicSourceId) ?? false;
    }
    
    public void ClearQueue()
    {
        _trackQueue.Clear();
        _currentTrack = null;
    }
    
    public IReadOnlyList<string> GetActiveSources()
    {
        return _mixer?.GetActiveSourceIds().ToList() ?? [];
    }

    public IReadOnlyList<QueuedTrack> GetQueuedTracks()
    {
        return _trackQueue.ToList();
    }

    public QueuedTrack? GetCurrentTrack()
    {
        return _currentTrack;
    }

    public int GetQueueLength()
    {
        return _trackQueue.Count;
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
        _queueSignal?.Dispose();
    }

    public void Dispose()
    {
        DisconnectAsync().GetAwaiter().GetResult();
        _queueSignal?.Dispose();
    }

    public class SongSearchResult
    {
        public string Title { get; init; } = string.Empty;
        public int QueuePosition { get; init; }
    }
}