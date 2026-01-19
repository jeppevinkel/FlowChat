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

    public enum RollType
    {
        Normal,
        Advantage,
        Disadvantage
    }

    [SlashCommand("roll", "Rolls a dnd dice")]
    [IntegrationType(ApplicationIntegrationType.UserInstall)]
    [CommandContextType(InteractionContextType.BotDm, InteractionContextType.Guild, InteractionContextType.PrivateChannel)]
    public async Task RollCommand(
        DiceType dice, 
        [Summary(description: "Reason for rolling the dice")] string? reason = null,
        [Summary(description: "Roll with advantage or disadvantage")] RollType type = RollType.Normal)
    {
        int firstRoll = _random.Next(1, (int)dice + 1);
        int result = firstRoll;
        string rollDetails = $"**{firstRoll}**";

        if (type != RollType.Normal)
        {
            int secondRoll = _random.Next(1, (int)dice + 1);
            if (type == RollType.Advantage)
            {
                result = Math.Max(firstRoll, secondRoll);
            }
            else
            {
                result = Math.Min(firstRoll, secondRoll);
            }
            rollDetails = $"({firstRoll}, {secondRoll}) -> **{result}**";
        }

        string response = $"Rolling {dice} with {type}: {rollDetails}";
        if (type == RollType.Normal)
        {
            response = $"Rolling {dice}: {rollDetails}";
        }

        if (!string.IsNullOrWhiteSpace(reason))
        {
            response += $" for {reason}";
        }
        await RespondAsync(response);
    }
}
