using EvasiveWhisker.Cards;

public interface ITrapEffect
{
    void Execute(ulong thiefId, ulong victimId, NetworkCard trapCard);
}