using EvasiveWhisker.Cards;

public interface IInteractiveTrapEffect
{
    void Begin(ulong thiefId, ulong victimId, NetworkCard trapCard, System.Action onFinished);
}