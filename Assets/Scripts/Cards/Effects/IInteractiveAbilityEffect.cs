using System;
using EvasiveWhisker.Cards;
public interface IInteractiveAbilityEffect
{
    void Begin(ulong senderId, ulong targetId, NetworkCard card, Action onFinished);
}