using EvasiveWhisker.Cards;

public class BombasticCatEyesEffect : IInteractiveAbilityEffect
{
    public void Begin(ulong senderId, ulong targetId, NetworkCard card, System.Action onFinished)
    {
        GameManager.Instance.BeginBombastic(senderId, targetId, card, onFinished);
    }
}