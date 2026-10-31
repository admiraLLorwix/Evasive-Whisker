using UnityEngine;
using EvasiveWhisker.Cards;

public class NineLivesEffect : IAbilityEffect
{
    public void Execute(ulong senderId, ulong targetId, NetworkCard card)
    {
        string senderName = GameManager.Instance.GetPlayerName(senderId);
        Debug.Log($"<color=green>[ninelives] {senderName} put Nine Lives into Active Ability Slot</color>");

        GameManager.Instance.EquipActiveAbility(senderId, card, 2);

        GameManager.Instance.SetTrapImmunity(senderId, true);
    }
}