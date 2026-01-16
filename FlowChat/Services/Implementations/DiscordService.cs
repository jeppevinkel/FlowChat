using Anthropic.SDK;
using Anthropic.SDK.Common;
using Anthropic.SDK.Constants;
using Anthropic.SDK.Messaging;
using Discord;
using Discord.WebSocket;
using FlowChat.Models;
using FlowChat.Tools;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Tool = Anthropic.SDK.Common.Tool;

namespace FlowChat.Services.Implementations;

public class DiscordService : IHostedService
{
    private readonly ILogger<DiscordService> _logger;
    private readonly IServiceProvider _services;
    private readonly IConfiguration _config;
    private readonly DiscordSocketClient _discordClient;
    private readonly Dictionary<ulong, ChannelContext> _channelContexts = new();
    private readonly AnthropicClient _anthropicClient;
    private string _systemPrompt = string.Empty;

    public DiscordService(ILogger<DiscordService> logger, IServiceProvider services, IConfiguration config, DiscordSocketClient discordClient)
    {
        _logger = logger;
        _services = services;
        _config = config;
        _discordClient = discordClient;
        _anthropicClient = new AnthropicClient(_config.GetValue<string>("ANTHROPIC_API_KEY"));
        
        _discordClient.Log += Log;
        _discordClient.Ready += () =>
        {
            _logger.LogInformation("Discord client is signed in as {Username}", _discordClient.CurrentUser.Username);
            
            return Task.CompletedTask;
        };
        _discordClient.MessageReceived += message =>
        {
            Task.Run(() => OnMessageReceived(message));
            
            return Task.CompletedTask;
        };
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory("config");
        if (File.Exists("./config/systemprompt.txt"))
        {
            _systemPrompt = await File.ReadAllTextAsync("./config/systemprompt.txt", cancellationToken);
        }
        else
        {
            _logger.LogWarning("System prompt is empty. Create a file called 'systemprompt.txt' in the program dir to add a system prompt");
        }
        
        await _discordClient.LoginAsync(TokenType.Bot, _config.GetValue<string>("DISCORD_TOKEN"));
        await _discordClient.StartAsync();
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _discordClient.LogoutAsync();
    }

    private async Task OnMessageReceived(SocketMessage message)
    {
        if (message.Author.IsBot || message.Author == _discordClient!.CurrentUser ||
            string.IsNullOrEmpty(message.CleanContent))
        {
            return;
        }

        if (!_channelContexts.TryGetValue(message.Channel.Id, out ChannelContext? channelContext))
        {
            channelContext = new ChannelContext(_services, message);

            _channelContexts.Add(message.Channel.Id, channelContext);
        }

        List<Tool> tools =
        [
            Tool.GetOrCreateTool(channelContext.MemoryManager, nameof(MemoryManager.StoreMemory)),
            Tool.GetOrCreateTool(channelContext.MemoryManager, nameof(MemoryManager.SearchRelevantMemories)),
            Tool.GetOrCreateTool(channelContext.VoiceChannelTools, nameof(VoiceChannelTools.JoinVoiceChannel)),
            Tool.GetOrCreateTool(channelContext.VoiceChannelTools, nameof(VoiceChannelTools.LeaveVoiceChannel)),
            Tool.GetOrCreateTool(channelContext.VoiceChannelTools, nameof(VoiceChannelTools.PlayMusic)),
            Tool.GetOrCreateTool(channelContext.VoiceChannelTools, nameof(VoiceChannelTools.SkipMusic)),
            Tool.GetOrCreateTool(channelContext.VoiceChannelTools, nameof(VoiceChannelTools.GetQueue)),
            Tool.GetOrCreateTool(channelContext.VoiceChannelTools, nameof(VoiceChannelTools.SetMusicVolume)),
            Tool.GetOrCreateTool(channelContext.VoiceChannelTools, nameof(VoiceChannelTools.SayInVoiceChannel))
        ];

        var parameters = new MessageParameters()
        {
            Messages = channelContext.Messages,
            MaxTokens = 2048,
            Model = AnthropicModels.Claude37Sonnet,
            Stream = false,
            Temperature = 1.0m,
            Tools = tools.ToList(),
            System =
            [
                new SystemMessage($"""
                                  {_systemPrompt}
                                  
                                  The current time is: {message.Timestamp}
                                  """)
            ]
        };
        _logger.LogInformation("[{Timestamp}] {Username}: {MessageContent}", message.Timestamp, message.Author.Username, message.CleanContent);

        channelContext.Messages.Add(new Message(RoleType.User, $"[{message.Timestamp}] {message.Author.Username}: {message.CleanContent}"));

        MessageResponse? res = await _anthropicClient.Messages.GetClaudeMessageAsync(parameters);
        channelContext.Messages.Add(res.Message);

        while (res.ToolCalls.Count > 0)
        {
            if (!string.IsNullOrEmpty(res.Message))
            {
                _logger.LogInformation("BeforeTool: {Response}", res.Message);
                await message.Channel.SendMessageAsync(res.Message.ToString());
            }
            
            foreach (Function? toolCall in res.ToolCalls)
            {
                try
                {
                    var response = await toolCall.InvokeAsync<string>();
                    
                    _logger.LogInformation("Toolcall: {ToolName}, result: {Result}", toolCall.Name, response);

                    channelContext.Messages.Add(new Message(toolCall, response));
                } catch (Exception ex)
                {
                    _logger.LogError(ex, "Error invoking tool call {ToolName}", toolCall.Name);
                    channelContext.Messages.Add(new Message(toolCall, ex.Message));
                }
            }

            res = await _anthropicClient.Messages.GetClaudeMessageAsync(parameters);
            
            channelContext.Messages.Add(res.Message);
        }

        _logger.LogInformation("Response: {Response}", res.Message.ToString());
        if (!string.IsNullOrEmpty(res.Message.ToString()))
        {
            await message.Channel.SendMessageAsync(res.Message.ToString());
        }
    }
    
    private Task Log(LogMessage msg)
    {
        switch (msg.Severity)
        {
            case LogSeverity.Info:
                _logger.LogInformation("{Message}", msg);
                break;
            case LogSeverity.Debug:
                _logger.LogDebug("{Message}", msg);
                break;
            case LogSeverity.Critical:
                _logger.LogCritical("{Message}", msg);
                break;
            case LogSeverity.Error:
                _logger.LogError("{Message}", msg);
                break;
            case LogSeverity.Warning:
                _logger.LogWarning("{Message}", msg);
                break;
            case LogSeverity.Verbose:
                _logger.LogTrace("{Message}", msg);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(msg));
        }

        return Task.CompletedTask;
    }
}