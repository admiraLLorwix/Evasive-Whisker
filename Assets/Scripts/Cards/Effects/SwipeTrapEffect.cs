using EvasiveWhisker.Cards;

public class SwipeTrapEffect : IInteractiveTrapEffect
{
    public void Begin(ulong thiefId, ulong victimId, NetworkCard trapCard, System.Action onFinished)
    {
        GameManager.Instance.BeginTrapGive(thiefId, victimId, trapCard, onFinished);
    }
}