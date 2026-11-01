using EvasiveWhisker.Cards;

public class CatFlashTrapEffect : IInteractiveTrapEffect
{
    public void Begin(ulong thiefId, ulong victimId, NetworkCard trapCard, System.Action onFinished)
    {
        GameManager.Instance.BeginCatFlash(thiefId, victimId, trapCard, onFinished);
    }
}