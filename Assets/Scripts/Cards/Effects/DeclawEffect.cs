using UnityEngine;
using System.Collections;
using EvasiveWhisker.Cards;

public class DeclawEffect : IAbilityEffect
{
    public void Execute(ulong senderId, ulong targetId, NetworkCard card)
    {
        string activeAbilityId = GameManager.Instance.GetPlayerActiveAbilityId(targetId);

        if (activeAbilityId == "CatNap")
        {
            Debug.LogWarning($"[declaw] cant disarm CatNap");
            GameManager.Instance.PlayTargetedAbilityFlightClientRpc(card.CardId, "", targetId, true);
        }
        else
        {
            GameManager.Instance.DisarmPlayerActiveAbility(targetId);
            GameManager.Instance.PlayTargetedAbilityFlightClientRpc(card.CardId, activeAbilityId, targetId, true);
        }

        GameManager.Instance.StartCoroutine(DelayedDiscard(card));
    }

    private IEnumerator DelayedDiscard(NetworkCard card)
    {
        yield return new WaitForSeconds(1.6f);
        DeckManager.Instance.Discard(card);
    }
}