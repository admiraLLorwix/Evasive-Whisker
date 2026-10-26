using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;

public class StealManager : NetworkBehaviour
{
    public static StealManager Instance { get; private set; }

    [Header("steal ui")]
    [SerializeField] private GameObject stealModal;
    [SerializeField] private TMP_Text stealTargetTitleText;
    [SerializeField] private Transform cardFanContainer;
    [SerializeField] private GameObject faceDownStealCardPrefab;
    [SerializeField] private Button stealCancelButton;

    private ulong currentStealTargetId;
    private bool isAccusingCustodian = false;

    private void Awake()
    {
        Instance = this;
        if (stealModal != null) stealModal.SetActive(false);

        if (stealCancelButton != null)
        {
            stealCancelButton.onClick.RemoveAllListeners();
            stealCancelButton.onClick.AddListener(OnStealCancelled);
        }
    }

    public void CancelSelection()
    {
        HighlightOpponentIslands(false);
        if (stealModal != null) stealModal.SetActive(false);
    }

    private void OnStealCancelled()
    {
        if (stealModal != null) stealModal.SetActive(false);
        if (TurnActionUI.Instance != null)
        {
            TurnActionUI.Instance.ShowActionPrompt();
        }
    }

    public void EnableOpponentSelection()
    {
        isAccusingCustodian = false;
        HighlightOpponentIslands(true, new Color(0f, 0.8f, 1f, 1f));
    }

    public void EnableCustodianAccusationSelection()
    {
        isAccusingCustodian = true;
        HighlightOpponentIslands(true, new Color(1f, 0.2f, 0.2f, 1f));
    }

    private void HighlightOpponentIslands(bool enable, Color glowColor = default)
    {
        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client.ClientId == NetworkManager.Singleton.LocalClientId) continue;

            var island = BoardLayoutManager.Instance.GetOpponentIsland(client.ClientId);
            if (island != null)
            {
                island.SetIslandGlow(enable, glowColor);

                Button btn = island.GetComponent<Button>();
                if (btn == null) btn = island.gameObject.AddComponent<Button>();

                btn.interactable = enable;
                ulong targetId = client.ClientId;

                btn.onClick.RemoveAllListeners();
                if (enable)
                {
                    btn.onClick.AddListener(() => OnOpponentIslandClicked(targetId));
                }
            }
        }
    }

    private void OnOpponentIslandClicked(ulong targetId)
    {
        Debug.Log($"[StealManager]Chose Player #{targetId}");
        HighlightOpponentIslands(false);

        if (isAccusingCustodian)
        {
            GameManager.Instance.SubmitCustodyGuessServerRpc(targetId);
            return;
        }

        RequestVictimHandForStealServerRpc(targetId);
    }

    [Rpc(SendTo.Server)]
    private void RequestVictimHandForStealServerRpc(ulong victimId, RpcParams rpcParams = default)
    {
        ulong thiefId = rpcParams.Receive.SenderClientId;
        int victimCardCount = GameManager.Instance.GetPlayerHandCount(victimId);

        Debug.Log($"[Server] Player #{thiefId} attempting to steal from #{victimId}. Victim has {victimCardCount} cards.");

        if (victimCardCount <= 0) victimCardCount = 3;

        OpenStealModalClientRpc(victimId, victimCardCount, new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { thiefId } }
        });
    }

    [ClientRpc]
    private void OpenStealModalClientRpc(ulong victimId, int cardCount, ClientRpcParams clientRpcParams = default)
    {
        Debug.Log($"<color=yellow>[StealModal] Victim: {victimId}, Cards: {cardCount}</color>");
        currentStealTargetId = victimId;

        if (stealModal == null)
        {
            Debug.LogError("[StealManager] !stealModal");
            return;
        }

        stealModal.SetActive(true);
        stealModal.transform.SetAsLastSibling();

        if (stealTargetTitleText != null)
        {
            stealTargetTitleText.text = $"Choose a card to steal from Player #{victimId} ({cardCount} cards)";
        }

        if (cardFanContainer == null)
        {
            Debug.LogError("[StealManager] !cardFanContainer");
            return;
        }

        foreach (Transform child in cardFanContainer)
        {
            Destroy(child.gameObject);
        }

        if (faceDownStealCardPrefab == null)
        {
            Debug.LogError("[StealManager] !faceDownStealCardPrefab");
            return;
        }

        for (int thiefSlotIndex = 0; thiefSlotIndex < cardCount; thiefSlotIndex++)
        {
            int actualVictimIndex = (cardCount - 1) - thiefSlotIndex;

            GameObject cardObj = Instantiate(faceDownStealCardPrefab, cardFanContainer);
            Button btn = cardObj.GetComponentInChildren<Button>();

            int selectedIndex = actualVictimIndex;
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => OnStealCardSelected(selectedIndex));
            }
        }
    }

    private void OnStealCardSelected(int victimCardIndex)
    {
        Debug.Log($"[Steal] Thief selected victim card index {victimCardIndex}!");
        if (stealModal != null) stealModal.SetActive(false);

        SubmitStealPickServerRpc(currentStealTargetId, victimCardIndex);
    }

    [Rpc(SendTo.Server)]
    private void SubmitStealPickServerRpc(ulong victimId, int cardIndex, RpcParams rpcParams = default)
    {
        ulong thiefId = rpcParams.Receive.SenderClientId;
        GameManager.Instance.ExecuteSteal(thiefId, victimId, cardIndex);
    }
}