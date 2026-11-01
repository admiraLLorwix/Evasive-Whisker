using EvasiveWhisker.Cards;

public class CopyCatEffect : ICounterplayEffect
{
    public bool CanCounter(string actionType) => actionType == "Reveal";

    public void Execute(ulong defenderId, ulong attackerId, NetworkCard counterCard)
    {
        GameManager.Instance.QueueCopyCat(defenderId, attackerId);
        GameManager.Instance.CancelPendingReaction(false);
        DeckManager.Instance.Discard(counterCard);
    }
}