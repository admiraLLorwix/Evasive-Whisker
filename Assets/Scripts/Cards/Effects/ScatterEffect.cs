using EvasiveWhisker.Cards;

public class ScatterEffect : IInteractiveAbilityEffect
{
    public void Begin(ulong senderId, ulong targetId, NetworkCard card, System.Action onFinished)
    {
        GameManager.Instance.BeginScatter(senderId, card, onFinished);
    }
}