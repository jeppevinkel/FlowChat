using System.Collections.Concurrent;
using CliWrap;
using Discord;
using Discord.Audio;
using Discord.WebSocket;
using FlowChat.Services.Interfaces;
using HostInitActions;
using Microsoft.Extensions.Logging;
using YoutubeExplode;
using YoutubeExplode.Search;
using YoutubeExplode.Videos.Streams;

namespace FlowChat;

public class VoiceChannelContext : IAsyncInitActionExecutor
{
    private readonly ILogger<VoiceChannelContext> _logger;
    private readonly DiscordSocketClient _discordClient;
    private readonly IBackgroundTaskQueue _musicQueue;
    private SocketGuild? _guild;
    private SocketVoiceChannel? _voiceChannel;
    private IAudioClient? _voiceConnection;

    public bool IsPlaying { get; private set; }

    public VoiceChannelContext(ILogger<VoiceChannelContext> logger, DiscordSocketClient discordClient,
        IBackgroundTaskQueue musicQueue)
    {
        _logger = logger;
        _discordClient = discordClient;
        _musicQueue = musicQueue;
    }

    public async Task ConnectVoiceAsync(IVoiceChannel voiceChannel)
    {
        await ConnectVoiceAsync(voiceChannel.Guild.Id, voiceChannel.Id);
    }

    public async Task ConnectVoiceAsync(ulong guildId, ulong channelId)
    {
        if (_voiceChannel is not null)
        {
            await _voiceChannel.DisconnectAsync();
            _voiceChannel = null;
            _guild = null;
            _voiceChannel = null;
        }

        try
        {
            _guild = _discordClient.GetGuild(guildId);
            _voiceChannel = _guild.GetVoiceChannel(channelId);
            _voiceConnection = await _voiceChannel.ConnectAsync();
            _logger.LogInformation("Connected to voice ({ChannelName})", _voiceChannel.Name);
        }
        catch (Exception e)
        {
            _logger.LogError("Error connecting to voice: {Error}", e);
            throw;
        }
    }

    /// <summary>
    /// Disconnect from the current channel if connected to any.
    /// </summary>
    public async Task DisconnectVoiceAsync()
    {
        if (_voiceChannel is not null)
        {
            await _voiceChannel.DisconnectAsync();
            _voiceChannel = null;
            _guild = null;
            _voiceChannel = null;
            // await _musicQueue.ClearAsync();
        }
    }

    /// <summary>
    /// Returns true of currently connected to a channel.
    /// </summary>
    /// <returns></returns>
    public bool IsConnected()
    {
        return _voiceChannel is not null;
    }

    /// <summary>
    /// Returns the name of the currently connected channel, null if not connected.
    /// </summary>
    /// <returns></returns>
    public string? ConnectedChannel()
    {
        return _voiceChannel?.Name;
    }

    /// <summary>
    /// Searches for a video and plays the audio.
    /// </summary>
    /// <param name="searchQuery"></param>
    /// <param name="cancellationToken"></param>
    /// <returns>The title of the video.</returns>
    public async Task<SongSearchResult> SearchMusic(string searchQuery, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Searching for: {SearchQuery}", searchQuery);
        var youtube = new YoutubeClient();
        VideoSearchResult result = await youtube.Search.GetVideosAsync(searchQuery, cancellationToken)
            .FirstAsync(cancellationToken);
        _logger.LogInformation("Found: {Name}, {Url}", result.Title, result.Url);

        await _musicQueue.QueueBackgroundWorkItemAsync(async (stoppingToken) =>
        {
            await PlayMusic(result.Url, stoppingToken);
        });

        return new SongSearchResult
        {
            Title = result.Title,
            QueuePosition = _musicQueue.Count() - 1 + (IsPlaying ? 1 : 0)
        };
    }

    public class SongSearchResult
    {
        public string Title { get; init; } = string.Empty;
        public int QueuePosition { get; init; }
    }

    public async ValueTask PlayMusic(string url, CancellationToken cancellationToken = default)
    {
        IsPlaying = true;
        try
        {
            var youtube = new YoutubeClient();
            StreamManifest streamManifest =
                await youtube.Videos.Streams.GetManifestAsync(url,
                    cancellationToken);
            IStreamInfo streamInfo = streamManifest.GetAudioOnlyStreams().GetWithHighestBitrate();
            Stream stream = await youtube.Videos.Streams.GetAsync(streamInfo, cancellationToken);

            var memoryStream = new MemoryStream();
            await Cli.Wrap("ffmpeg")
                .WithArguments(" -hide_banner -loglevel panic -i pipe:0 -ac 2 -f s16le -ar 48000 pipe:1")
                .WithStandardInputPipe(PipeSource.FromStream(stream))
                .WithStandardOutputPipe(PipeTarget.ToStream(memoryStream))
                .ExecuteAsync(cancellationToken);

            await using AudioOutStream? audioOutStream = _voiceConnection!.CreatePCMStream(AudioApplication.Mixed);

            try
            {
                await audioOutStream.WriteAsync(memoryStream.ToArray().AsMemory(0, (int) memoryStream.Length),
                    cancellationToken);
            }
            catch (Exception e)
            {
                IsPlaying = false;
                throw;
            }
            finally
            {
                await audioOutStream.FlushAsync(cancellationToken);
                IsPlaying = false;
            }
        }
        catch (TaskCanceledException e)
        {
            _logger.LogWarning("Music cancelled: {Exception}", e);
            IsPlaying = false;
        }
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Readying voice channel contact");
        await BackgroundProcessing(cancellationToken);
    }

    private async Task BackgroundProcessing(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var workItem =
                await _musicQueue.DequeueAsync(stoppingToken);

            try
            {
                await workItem(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error occurred executing {WorkItem}", nameof(workItem));
            }
        }
    }
}