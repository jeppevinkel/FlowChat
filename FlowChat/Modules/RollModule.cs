using Discord;
using Discord.Interactions;
using FlowChat.Helpers;

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
        int? secondRoll = null;
        int result = firstRoll;

        switch (type)
        {
            case RollType.Normal:
                break;
            case RollType.Advantage:
                secondRoll = _random.Next(1, (int)dice + 1);
                result = Math.Max(firstRoll, secondRoll.Value);
                break;
            case RollType.Disadvantage:
                secondRoll = _random.Next(1, (int)dice + 1);
                result = Math.Min(firstRoll, secondRoll.Value);
                break;
        }

        Embed responseEmbed = EmbedFactory.CreateDiceRollEmbed(dice.ToString(), result, (int) dice, firstRoll, secondRoll, reason: reason,
            rollerName: Context.User.Username, rollTypeName: type == RollType.Normal ? null : type.ToString());
        await RespondAsync(embed: responseEmbed);
    }
}
