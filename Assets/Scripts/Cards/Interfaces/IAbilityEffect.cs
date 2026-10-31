using EvasiveWhisker.Cards;

public interface IAbilityEffect
{
    void Execute(ulong senderId, ulong targetId, NetworkCard card);
}