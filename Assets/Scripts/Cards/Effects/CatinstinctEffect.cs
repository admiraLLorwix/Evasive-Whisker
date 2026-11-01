using EvasiveWhisker.Cards;

public class CatInstinctEffect : IInteractiveAbilityEffect
{
    public void Begin(ulong senderId, ulong targetId, NetworkCard card, System.Action onFinished)
    {
        GameManager.Instance.BeginCatInstinct(senderId, targetId, card, onFinished);
    }
}