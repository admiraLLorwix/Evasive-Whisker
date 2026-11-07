using UnityEngine;
using EvasiveWhisker.Cards;

public class TargetingManager : MonoBehaviour
{
    public static TargetingManager Instance { get; private set; }

    public bool IsTargeting { get; private set; } = false;
    private NetworkCard pendingAbilityCard;
    private GameObject pendingCardObj;

    [SerializeField] private GameObject globalDimPanel; 

    private void Awake()
    {
        Instance = this;
        if (globalDimPanel != null) globalDimPanel.SetActive(false);
    }

    public void StartTargeting(NetworkCard card, GameObject cardObj)
    {
        IsTargeting = true;
        pendingAbilityCard = card;
        pendingCardObj = cardObj;

        if (globalDimPanel != null) globalDimPanel.SetActive(true);
        TurnActionUI.Instance?.ShowTemporaryNotification($"<color=yellow>Click an opponent's island to target with {card.CardId}</color>", 8f);

        HighlightIslands(true);
        GameManager.Instance?.ReportDecisionStepServerRpc((int)GameManager.DecisionStep.AbilityTarget, card.CardId);
    }

    public void OnOpponentTargetChosen(ulong targetId)
    {
        if (!IsTargeting) return;
        IsTargeting = false;
        if (globalDimPanel != null) globalDimPanel.SetActive(false);
        HighlightIslands(false);

        Debug.Log($"[targeting mngr] target #{targetId} confirmed for {pendingAbilityCard.CardId}!");

        if (pendingCardObj != null)
        {
            pendingCardObj.SetActive(false);
            PlayerHandController.Instance?.RemoveCardObject(pendingCardObj, pendingAbilityCard);
        }

        TurnActionUI.Instance?.HideAllActions();
        GameManager.Instance?.SubmitPlayAbilityCardServerRpc(pendingAbilityCard, targetId);
    }

    public void ForceStop()
    {
        if (!IsTargeting) return;
        IsTargeting = false;
        if (globalDimPanel != null) globalDimPanel.SetActive(false);
        HighlightIslands(false);
    }

    public void CancelTargeting()
    {
        IsTargeting = false;
        if (globalDimPanel != null) globalDimPanel.SetActive(false);
        HighlightIslands(false);
        TurnActionUI.Instance?.ShowActionPrompt();
    }

    private void HighlightIslands(bool enable)
    {
        foreach (var client in Unity.Netcode.NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client.ClientId == Unity.Netcode.NetworkManager.Singleton.LocalClientId) continue;
            var island = BoardLayoutManager.Instance != null ? BoardLayoutManager.Instance.GetOpponentIsland(client.ClientId) : null;
            if (island != null)
            {
                island.SetTargetable(enable);
            }
        }
    }
}