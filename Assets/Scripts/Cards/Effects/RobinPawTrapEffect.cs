using EvasiveWhisker.Cards;

public class RobinPawTrapEffect : IInteractiveTrapEffect
{
    public void Begin(ulong thiefId, ulong victimId, NetworkCard trapCard, System.Action onFinished)
    {
        ulong recipientId = GameManager.Instance.GetRobinPawRecipient(thiefId);
        GameManager.Instance.BeginTrapGive(thiefId, recipientId, trapCard, onFinished);
    }
}