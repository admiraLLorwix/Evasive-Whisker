using EvasiveWhisker.Cards;

public class CatNapTrapEffect : ITrapEffect
{
    public void Execute(ulong thiefId, ulong victimId, NetworkCard trapCard)
    {
        GameManager.Instance.EquipActiveAbility(thiefId, trapCard, 2);
    }
}