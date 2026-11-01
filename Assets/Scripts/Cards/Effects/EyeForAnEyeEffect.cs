using EvasiveWhisker.Cards;

public class EyeForAnEyeEffect : ICounterplayEffect
{
    public bool CanCounter(string actionType) => actionType == "Steal";

    public void Execute(ulong defenderId, ulong attackerId, NetworkCard counterCard)
    {
        GameManager.Instance.QueueEyeForAnEye(defenderId, attackerId);
        DeckManager.Instance.Discard(counterCard);
    }
}