using Discord.WebSocket;
using FlowChat.Services.Implementations;
using FlowChat.Services.Interfaces;
using HostInitActions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FlowChat;

class Program
{
    static async Task Main(string[] args)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

        var discordConfig = new DiscordSocketConfig()
        {
            
        };

        builder.Services.AddSingleton(discordConfig).AddSingleton<DiscordSocketClient>();

        builder.Services.AddSingleton<VoiceChannelContext>();
        builder.Services.AddHostedService<DiscordService>();
        builder.Services.AddTransient<ElevenLabsService>();
        
        builder.Services.AddSingleton<IBackgroundTaskQueue>(ctx =>
        {
            if (!int.TryParse(builder.Configuration["QueueCapacity"], out var queueCapacity))
                queueCapacity = 100;
            return new BackgroundTaskQueue(queueCapacity);
        });
        
        builder.Services.AddHostedService<VoiceChannelBackgroundService>();

        using IHost host = builder.Build();
        await host.RunAsync();
    }
}