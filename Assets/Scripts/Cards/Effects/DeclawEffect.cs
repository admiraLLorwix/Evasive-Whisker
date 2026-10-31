using UnityEngine;
using EvasiveWhisker.Cards;

public class DeclawEffect : IAbilityEffect
{
    public void Execute(ulong senderId, ulong targetId, NetworkCard card)
    {
        string senderName = GameManager.Instance.GetPlayerName(senderId);
        string targetName = GameManager.Instance.GetPlayerName(targetId);

        string activeAbilityId = GameManager.Instance.GetPlayerActiveAbilityId(targetId);

        if (activeAbilityId == "CatNap")
        {
            Debug.LogWarning($"[declaw] cant disarm CatNap");
        }
        else
        {
            Debug.Log($"<color=orange>[declaw] {senderName} disarmed {targetName}'s {activeAbilityId}</color>");
            GameManager.Instance.DisarmPlayerActiveAbility(targetId);
        }

        DeckManager.Instance.Discard(card);
    }
}