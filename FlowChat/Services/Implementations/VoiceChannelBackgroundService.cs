// using Microsoft.Extensions.Hosting;
// using Microsoft.Extensions.Logging;
//
// namespace FlowChat.Services.Implementations;
//
// public class VoiceChannelBackgroundService : BackgroundService
// {
//     private readonly VoiceChannelContext _voiceContext;
//     private readonly ILogger<VoiceChannelBackgroundService> _logger;
//
//     public VoiceChannelBackgroundService(
//         VoiceChannelContext voiceContext,
//         ILogger<VoiceChannelBackgroundService> logger)
//     {
//         _voiceContext = voiceContext;
//         _logger = logger;
//     }
//
//     protected override async Task ExecuteAsync(CancellationToken stoppingToken)
//     {
//         _logger.LogInformation("Starting voice channel background processor");
//         await _voiceContext.BackgroundProcessing(stoppingToken);
//     }
// }