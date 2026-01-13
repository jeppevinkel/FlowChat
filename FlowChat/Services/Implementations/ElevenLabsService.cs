using ElevenLabs;
using ElevenLabs.TextToSpeech;
using ElevenLabs.Voices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace FlowChat.Services.Implementations;

public class ElevenLabsService
{
    private readonly ILogger<ElevenLabsService> _logger;
    private readonly IServiceProvider _services;
    private readonly IConfiguration _config;
    private readonly ElevenLabsClient _elevenLabsClient;
    private Voice? _voice;

    public ElevenLabsService(ILogger<ElevenLabsService> logger, IServiceProvider services, IConfiguration config)
    {
        _logger = logger;
        _services = services;
        _config = config;
        
        _elevenLabsClient = new ElevenLabsClient(_config.GetValue<string>("ELEVENLAB_API_KEY"));
    }

    public async Task<Voice> GetVoice()
    {
        _voice ??= await _elevenLabsClient.VoicesEndpoint.GetVoiceAsync("21m00Tcm4TlvDq8ikWAM");

        return _voice;
    }
    
    public async Task<VoiceClip> GenerateTextToSpeechAsync(string text, CancellationToken cancellationToken = default)
    {
        Voice voice = await GetVoice();
        var request = new TextToSpeechRequest(voice, text, outputFormat: OutputFormat.MP3_44100_128);
        VoiceClip? response = await _elevenLabsClient.TextToSpeechEndpoint.TextToSpeechAsync(request, cancellationToken: cancellationToken);
        return response;
    }
    
    public async Task<VoiceClip> GenerateTextToSpeechStreamingAsync(string text, Func<VoiceClip, Task> partialClipCallback, CancellationToken cancellationToken = default)
    {
        Voice voice = await GetVoice();
        var request = new TextToSpeechRequest(voice, text, outputFormat: OutputFormat.MP3_44100_128);
        VoiceClip? response = await _elevenLabsClient.TextToSpeechEndpoint.TextToSpeechAsync(request, partialClipCallback, cancellationToken: cancellationToken);
        return response;
    }
}