//using System.Collections;
//using UnityEngine;
using EvasiveWhisker.Cards;

public class HissEffect : ICounterplayEffect
{
    public bool CanCounter(string actionType) => actionType == "Steal" || actionType == "Ability";

    public void Execute(ulong defenderId, ulong attackerId, NetworkCard counterCard)
    {
        GameManager.Instance.CancelPendingReaction(true);
        DeckManager.Instance.Discard(counterCard);
    }
}