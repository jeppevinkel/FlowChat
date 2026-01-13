using System.Collections.Concurrent;
using CliWrap;
using Discord.Audio;
using FlowChat.Models;

namespace FlowChat.Services.Implementations;

public class AudioMixingService : IDisposable
{
    private readonly ConcurrentDictionary<string, AudioSource> _sources = new();
    private readonly ConcurrentDictionary<string, TaskCompletionSource<bool>> _completionSources = new();
    private const int BufferSize = 3840; // 20ms at 48kHz stereo 16-bit (48000 * 2 * 2 / 50)
    private readonly SemaphoreSlim _mixerLock = new(1, 1);
    private CancellationTokenSource? _mixingCts;
    
    public async Task AddSourceAsync(string id, Stream inputStream, float volume = 1.0f, CancellationToken ct = default)
    {
        // Remove existing source with same ID if present
        if (_sources.ContainsKey(id))
        {
            RemoveSource(id);
            
            // Give the mixer loop time to clean up
            await Task.Delay(50, ct);
        }
        
        Stream pcmStream = await ConvertToPCMAsync(inputStream, ct);
        var source = new AudioSource(pcmStream, volume);
        
        await _mixerLock.WaitAsync(ct);
        try
        {
            _sources[id] = source;
            _completionSources[id] = new TaskCompletionSource<bool>();
        }
        finally
        {
            _mixerLock.Release();
        }
    }
    
    public Task WaitForSourceCompletionAsync(string id)
    {
        return _completionSources.TryGetValue(id, out var tcs) ? tcs.Task : Task.CompletedTask;
    }
    
    /// <summary>
    /// Removes the source with the given ID from the mixer.
    /// </summary>
    /// <param name="id"></param>
    /// <returns>True if the source was removed, false if it did not exist</returns>
    public bool RemoveSource(string id)
    {
        if (!_sources.TryRemove(id, out AudioSource? source)) return false;
        source.Dispose();
            
        // Signal completion
        if (_completionSources.TryRemove(id, out var tcs))
        {
            tcs.TrySetResult(true);
        }

        return true;
    }
    
    public void SetVolume(string id, float volume)
    {
        if (_sources.TryGetValue(id, out AudioSource? source))
        {
            source.Volume = volume;
        }
    }
    
    public int ActiveSourceCount => _sources.Count;
    
    // Add this new method
    public bool HasSource(string id) => _sources.ContainsKey(id);
    
    public IEnumerable<string> GetActiveSourceIds() => _sources.Keys;
    
    public async Task StartMixingAsync(AudioOutStream discordStream, CancellationToken ct = default)
    {
        _mixingCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        
        try
        {
            await MixingLoopAsync(discordStream, _mixingCts.Token);
        }
        catch (OperationCanceledException)
        {
            // Expected when cancellation is requested
        }
        finally
        {
            await discordStream.FlushAsync();
        }
    }
    
    private async Task MixingLoopAsync(AudioOutStream discordStream, CancellationToken ct)
    {
        byte[] mixBuffer = new byte[BufferSize];
        
        while (!ct.IsCancellationRequested)
        {
            await _mixerLock.WaitAsync(ct);
            try
            {
                Array.Clear(mixBuffer, 0, mixBuffer.Length);
                bool hasAudio = false;

                // Mix all active sources
                var sourcesToRemove = new List<string>();
                
                foreach ((var id, AudioSource source) in _sources)
                {
                    int bytesRead = await source.ReadAsync(mixBuffer.Length, ct);
                    
                    if (bytesRead == 0)
                    {
                        // Source has ended
                        sourcesToRemove.Add(id);
                        continue;
                    }
                    
                    MixInto(mixBuffer, source.Buffer, bytesRead, source.Volume);
                    hasAudio = true;
                }

                // Remove finished sources
                foreach (var id in sourcesToRemove)
                {
                    RemoveSource(id);
                }

                // Write mixed audio to Discord (or silence if no sources)
                await discordStream.WriteAsync(mixBuffer.AsMemory(0, BufferSize), ct);
                
                // Small delay to maintain consistent streaming rate
                if (!hasAudio)
                {
                    await Task.Delay(20, ct); // 20ms frames
                }
            }
            finally
            {
                _mixerLock.Release();
            }
        }
    }
    
    private void MixInto(byte[] target, byte[] source, int sourceLength, float volume)
    {
        for (int i = 0; i < Math.Min(target.Length, sourceLength) - 1; i += 2)
        {
            short targetSample = BitConverter.ToInt16(target, i);
            short sourceSample = BitConverter.ToInt16(source, i);
            
            int mixed = targetSample + (int)(sourceSample * volume);
            mixed = Math.Clamp(mixed, short.MinValue, short.MaxValue);
            
            BitConverter.TryWriteBytes(target.AsSpan(i), (short)mixed);
        }
    }
    
    private async Task<Stream> ConvertToPCMAsync(Stream inputStream, CancellationToken ct)
    {
        var memoryStream = new MemoryStream();
        
        await Cli.Wrap("ffmpeg")
            .WithArguments("-hide_banner -loglevel panic -i pipe:0 -ac 2 -f s16le -ar 48000 pipe:1")
            .WithStandardInputPipe(PipeSource.FromStream(inputStream))
            .WithStandardOutputPipe(PipeTarget.ToStream(memoryStream))
            .ExecuteAsync(ct);
        
        memoryStream.Position = 0;
        return memoryStream;
    }
    
    public void Dispose()
    {
        _mixingCts?.Cancel();
        _mixingCts?.Dispose();
        
        foreach (var source in _sources.Values)
        {
            source.Dispose();
        }
        
        _sources.Clear();
        
        foreach (var tcs in _completionSources.Values)
        {
            tcs.TrySetCanceled();
        }
        _completionSources.Clear();
        
        _mixerLock.Dispose();
    }
}