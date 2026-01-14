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

        var discordConfig = new DiscordSocketConfig();

        builder.Services.AddSingleton(discordConfig).AddSingleton<DiscordSocketClient>();
        
        // Register the manager as singleton
        builder.Services.AddSingleton<GuildContextManager>();

        // builder.Services.AddSingleton<VoiceChannelContext>();
        // builder.Services.AddHostedService<DiscordService>();
        
        // Register services that can be used per-guild
        builder.Services.AddTransient<ElevenLabsService>();
        
        // builder.Services.AddSingleton<IBackgroundTaskQueue>(ctx =>
        // {
        //     if (!int.TryParse(builder.Configuration["QueueCapacity"], out var queueCapacity))
        //         queueCapacity = 100;
        //     return new BackgroundTaskQueue(queueCapacity);
        // });
        
        // builder.Services.AddHostedService<VoiceChannelBackgroundService>();

        builder.Services.AddHostedService<DiscordService>();
        
        using IHost host = builder.Build();
        await host.RunAsync();
    }
}