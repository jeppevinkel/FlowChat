using Discord;
using Discord.Interactions;

namespace FlowChat.Modules;

public class RollModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly Random _random = new();

    public enum DiceType
    {
        D2 = 2,
        D3 = 3,
        D4 = 4,
        D6 = 6,
        D8 = 8,
        D10 = 10,
        D12 = 12,
        D20 = 20,
        D100 = 100
    }

    [SlashCommand("roll", "Rolls a dnd dice")]
    [IntegrationType(ApplicationIntegrationType.UserInstall)]
    [CommandContextType(InteractionContextType.BotDm, InteractionContextType.Guild, InteractionContextType.PrivateChannel)]
    public async Task RollCommand(DiceType dice, [Summary(description: "Reason for rolling the dice")] string? reason = null)
    {
        int result = _random.Next(1, (int)dice + 1);
        string response = $"Rolling {dice}: **{result}**";
        if (!string.IsNullOrWhiteSpace(reason))
        {
            response += $" for {reason}";
        }
        await RespondAsync(response);
    }
}
