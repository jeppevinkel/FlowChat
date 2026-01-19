using Discord.WebSocket;
using FlowChat.Services.Implementations;
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
        
        // Register services that can be used per-guild
        builder.Services.AddTransient<ElevenLabsService>();

        builder.Services.AddHostedService<DiscordService>();
        
        using IHost host = builder.Build();
        await host.RunAsync();
    }
}