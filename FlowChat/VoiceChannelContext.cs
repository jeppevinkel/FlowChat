// using System.Collections.Concurrent;
// using Discord;
// using Discord.Audio;
// using Discord.WebSocket;
// using ElevenLabs;
// using FlowChat.Models;
// using FlowChat.Services.Implementations;
// using Microsoft.Extensions.Logging;
// using YoutubeExplode;
// using YoutubeExplode.Search;
// using YoutubeExplode.Videos.Streams;
//
// namespace FlowChat;
//
// public class VoiceChannelContext : IDisposable
// {
//     private readonly ILogger<VoiceChannelContext> _logger;
//     private readonly DiscordSocketClient _discordClient;
//     private readonly ElevenLabsService _elevenLabsService;
//     
//     private SocketGuild? _guild;
//     private SocketVoiceChannel? _voiceChannel;
//     private IAudioClient? _voiceConnection;
//     private AudioMixingService? _mixer;
//     private CancellationTokenSource? _mixerCts;
//     
//     private readonly ConcurrentQueue<QueuedTrack> _trackQueue = new();
//     private readonly SemaphoreSlim _queueSignal = new(0);
//     private QueuedTrack? _currentTrack;
//     
//     private const string MusicSourceId = "music";
//
//     public bool IsPlaying => _mixer?.ActiveSourceCount > 0;
//     public bool IsMusicPlaying => _mixer?.HasSource(MusicSourceId) ?? false;
//
//     public VoiceChannelContext(
//         ILogger<VoiceChannelContext> logger,
//         DiscordSocketClient discordClient,
//         // IBackgroundTaskQueue musicQueue,
//         ElevenLabsService elevenLabsService)
//     {
//         _logger = logger;
//         _discordClient = discordClient;
//         // _musicQueue = musicQueue;
//         _elevenLabsService = elevenLabsService;
//     }
//
//     public async Task ConnectVoiceAsync(IVoiceChannel voiceChannel)
//     {
//         await ConnectVoiceAsync(voiceChannel.Guild.Id, voiceChannel.Id);
//     }
//
//     public async Task ConnectVoiceAsync(ulong guildId, ulong channelId)
//     {
//         if (_voiceChannel is not null)
//         {
//             await DisconnectVoiceAsync();
//         }
//
//         try
//         {
//             _guild = _discordClient.GetGuild(guildId);
//             _voiceChannel = _guild.GetVoiceChannel(channelId);
//             _voiceConnection = await _voiceChannel.ConnectAsync();
//             
//             // Initialize and start mixer
//             await StartMixerAsync();
//             
//             _logger.LogInformation("Connected to voice channel: ({ChannelName})", _voiceChannel.Name);
//         }
//         catch (Exception e)
//         {
//             _logger.LogError(e, "Error connecting to voice: {Error}", e);
//             throw;
//         }
//     }
//
//     private async Task StartMixerAsync()
//     {
//         if (_voiceConnection == null)
//         {
//             throw new InvalidOperationException("Voice connection not established");
//         }
//
//         _mixer = new AudioMixingService();
//         _mixerCts = new CancellationTokenSource();
//         
//         AudioOutStream? audioOutStream = _voiceConnection.CreatePCMStream(AudioApplication.Mixed);
//         
//         // Start the mixing loop in the background
//         _ = Task.Run(async () =>
//         {
//             try
//             {
//                 await _mixer.StartMixingAsync(audioOutStream, _mixerCts.Token);
//             }
//             catch (OperationCanceledException)
//             {
//                 _logger.LogInformation("Audio mixer stopped");
//             }
//             catch (Exception e)
//             {
//                 _logger.LogError(e, "Error in audio mixer");
//             }
//             finally
//             {
//                 await audioOutStream.DisposeAsync();
//             }
//         }, _mixerCts.Token);
//         
//         // Give the mixer a moment to start
//         await Task.Delay(100);
//     }
//
//     /// <summary>
//     /// Disconnect from the current channel if connected to any.
//     /// </summary>
//     public async Task DisconnectVoiceAsync()
//     {
//         if (_voiceChannel is not null)
//         {
//             // Stop mixer first
//             _mixerCts?.Cancel();
//             _mixer?.Dispose();
//             _mixer = null;
//             _mixerCts?.Dispose();
//             _mixerCts = null;
//             
//             // Disconnect from voice
//             await _voiceChannel.DisconnectAsync();
//             _voiceChannel = null;
//             _guild = null;
//             _voiceConnection = null;
//             
//             ClearQueue();
//             
//             _logger.LogInformation("Disconnected from voice channel");
//         }
//     }
//
//     /// <summary>
//     /// Returns true if currently connected to a channel.
//     /// </summary>
//     /// <returns></returns>
//     public bool IsConnected() => _voiceChannel is not null && _mixer is not null;
//
//     /// <summary>
//     /// Returns the name of the currently connected channel, null if not connected.
//     /// </summary>
//     /// <returns></returns>
//     public string? ConnectedChannel() => _voiceChannel?.Name;
//
//     /// <summary>
//     /// Searches for a video and plays the audio.
//     /// </summary>
//     /// <param name="searchQuery"></param>
//     /// <param name="cancellationToken"></param>
//     /// <returns>The title of the video.</returns>
//     public async Task<SongSearchResult> SearchMusic(string searchQuery, CancellationToken cancellationToken = default)
//     {
//         if (!IsConnected())
//             throw new InvalidOperationException("Not connected to a voice channel");
//         
//         _logger.LogInformation("Searching for: {SearchQuery}", searchQuery);
//         
//         var youtube = new YoutubeClient();
//         VideoSearchResult result = await youtube.Search.GetVideosAsync(searchQuery, cancellationToken)
//             .FirstAsync(cancellationToken);
//         
//         _logger.LogInformation("Found: {Title} - {Url}", result.Title, result.Url);
//         
//         // Create track metadata
//         var track = new QueuedTrack
//         {
//             Title = result.Title,
//             Url = result.Url,
//             Duration = result.Duration
//         };
//         
//         // Add to metadata queue
//         _trackQueue.Enqueue(track);
//         _queueSignal.Release();
//
//         return new SongSearchResult
//         {
//             Title = result.Title,
//             QueuePosition = _trackQueue.Count - 1 + (IsMusicPlaying ? 1 : 0)
//         };
//     }
//
//     public async ValueTask PlayMusic(QueuedTrack track, CancellationToken cancellationToken = default)
//     {
//         if (_mixer is null)
//         {
//             _logger.LogWarning("Attempted to play music but not connected to voice");
//             return;
//         }
//         
//         _currentTrack = track;
//
//         try
//         {
//             _logger.LogInformation("Starting music: {Title}", track.Title);
//
//             // Download audio stream
//             var youtube = new YoutubeClient();
//             StreamManifest streamManifest =
//                 await youtube.Videos.Streams.GetManifestAsync(track.Url,
//                     cancellationToken);
//             IStreamInfo streamInfo = streamManifest.GetAudioOnlyStreams().GetWithHighestBitrate();
//             Stream audioStream = await youtube.Videos.Streams.GetAsync(streamInfo, cancellationToken);
//
//             // Add to mixer (will be converted to PCM inside mixer)
//             await _mixer.AddSourceAsync(MusicSourceId, audioStream, volume: 0.2f, cancellationToken);
//
//             _logger.LogInformation("Now playing: {Title}", track.Title);
//
//             // Wait for the song to finish before returning (so next song can play)
//             await _mixer.WaitForSourceCompletionAsync(MusicSourceId);
//
//             _logger.LogInformation("Finished playing: {Title}", track.Title);
//         }
//         catch (OperationCanceledException)
//         {
//             _logger.LogWarning("Music playback cancelled: {Title}", track.Title);
//             _mixer?.RemoveSource(MusicSourceId);
//         }
//         catch (Exception e)
//         {
//             _logger.LogError(e, "Error playing music: {Title}", track.Title);
//             _mixer?.RemoveSource(MusicSourceId);
//         }
//         finally
//         {
//             _currentTrack = null;
//         }
//     }
//
//     public async Task PlayTextToSpeech(string text, CancellationToken cancellationToken = default)
//     {
//         if (_mixer == null)
//             throw new InvalidOperationException("Not connected to a voice channel");
//
//         try
//         {
//             _logger.LogInformation("Generating TTS: {Text}", text.Substring(0, Math.Min(50, text.Length)));
//             
//             // Generate TTS
//             VoiceClip voiceClip = await _elevenLabsService.GenerateTextToSpeechAsync(text, cancellationToken);
//             
//             // Create stream from bytes
//             var inputStream = new MemoryStream(voiceClip.ClipData.ToArray());
//             
//             // Add to mixer with high priority volume (plays over music)
//             var ttsId = $"tts_{Guid.NewGuid()}";
//             await _mixer.AddSourceAsync(ttsId, inputStream, volume: 1.0f, cancellationToken);
//             
//             _logger.LogInformation("TTS started: {Id}", ttsId);
//         }
//         catch (Exception e)
//         {
//             _logger.LogError(e, "Error playing TTS");
//             throw;
//         }
//     }
//     
//     public void SetMusicVolume(float volume)
//     {
//         _mixer?.SetVolume(MusicSourceId, Math.Clamp(volume, 0f, 1f));
//         _logger.LogInformation("Music volume set to {Volume}", volume);
//     }
//     
//     public bool SkipCurrentSong()
//     {
//         bool skipped = _mixer?.RemoveSource(MusicSourceId) ?? false;
//         if (skipped)
//         {
//             _logger.LogInformation("Skipped current song: {Title}", _currentTrack?.Title ?? "Unknown");
//         }
//         
//         return skipped;
//     }
//     
//     public void ClearQueue()
//     {
//         _trackQueue.Clear();
//         _currentTrack = null;
//         _logger.LogInformation("Cleared music queue");
//     }
//     
//     public string[] GetActiveSources()
//     {
//         return _mixer?.GetActiveSourceIds().ToArray() ?? [];
//     }
//     
//     public IReadOnlyList<QueuedTrack> GetQueuedTracks()
//     {
//         return _trackQueue.ToList();
//     }
//
//     public QueuedTrack? GetCurrentTrack()
//     {
//         return _currentTrack;
//     }
//
//     public int GetQueueLength()
//     {
//         return _trackQueue.Count;
//     }
//
//     public async Task BackgroundProcessing(CancellationToken stoppingToken)
//     {
//         while (!stoppingToken.IsCancellationRequested)
//         {
//             try
//             {
//                 // Wait for a track to be queued
//                 await _queueSignal.WaitAsync(stoppingToken);
//                 
//                 // Dequeue and play the track
//                 if (_trackQueue.TryDequeue(out QueuedTrack? track))
//                 {
//                     await PlayMusic(track, stoppingToken);
//                 }
//             }
//             catch (OperationCanceledException)
//             {
//                 // Expected on shutdown
//                 break;
//             }
//             catch (Exception e)
//             {
//                 _logger.LogError(e, "Error executing music playback");
//             }
//         }
//         
//         _logger.LogInformation("Voice channel background processor stopped");
//     }
//     
//     public void Dispose()
//     {
//         _mixerCts?.Cancel();
//         _mixer?.Dispose();
//         _mixerCts?.Dispose();
//         _queueSignal?.Dispose();
//     }
//
//     public class SongSearchResult
//     {
//         public string Title { get; init; } = string.Empty;
//         public int QueuePosition { get; init; }
//     }
// }