using Discord;
using Discord.Interactions;

namespace FlowChat.Modules;

public class RollModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly Random _random = new();

    public enum DiceType
    {
        D4 = 4,
        D6 = 6,
        D8 = 8,
        D20 = 20
    }

    [SlashCommand("roll", "Rolls a dnd dice")]
    [IntegrationType(ApplicationIntegrationType.UserInstall)]
    [CommandContextType(InteractionContextType.BotDm, InteractionContextType.Guild, InteractionContextType.PrivateChannel)]
    public async Task RollCommand(DiceType dice)
    {
        int result = _random.Next(1, (int)dice + 1);
        await RespondAsync($"Rolling {dice}: **{result}**");
    }
}
