using UnityEngine;
using EvasiveWhisker.Cards;

public class ZoomiesEffect : IAbilityEffect
{
    public void Execute(ulong senderId, ulong targetId, NetworkCard card)
    {
        string senderName = GameManager.Instance.GetPlayerName(senderId);
        Debug.Log($"<color=cyan>[zoomies] {senderName} used Zoomies, gets 2 actions on next turn</color>");

        GameManager.Instance.EquipActiveAbility(senderId, card, 2);

        GameManager.Instance.SetPendingZoomies(senderId, true);
    }
}