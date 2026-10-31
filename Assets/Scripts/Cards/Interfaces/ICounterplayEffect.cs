using EvasiveWhisker.Cards;

public interface ICounterplayEffect
{
    bool CanCounter(string actionType);
    void Execute(ulong defenderId, ulong attackerId, NetworkCard counterCard);
}