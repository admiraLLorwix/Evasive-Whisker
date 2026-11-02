using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;
using EvasiveWhisker.Cards;

public class StealManager : NetworkBehaviour
{
    public static StealManager Instance { get; private set; }

    [Header("steal ui")]
    [SerializeField] private GameObject stealModal;
    [SerializeField] private Image victimAvatarImage;
    [SerializeField] private TMP_Text victimNameText;
    [SerializeField] private Transform cardFanContainer;
    [SerializeField] private GameObject stealCardPrefab;
    [SerializeField] private Button stealCancelButton;

    [SerializeField] private float cardSpacingX = 85f;
    [SerializeField] private float maxTiltAngle = 10f;
    [SerializeField] private float arcDropAmount = 14f;

    private ulong currentStealTargetId;
    public bool IsSelectingTarget { get; set; } = false;
    private bool isAccusingCustodian = false;
    private bool isIslandPick = false;
    private HashSet<ulong> islandPickEligible = new HashSet<ulong>();
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
    //decoy mokang
    public void BeginIslandPick(ulong[] eligible, string prompt, bool allowCancel = false)
    {
        isIslandPick = true;
        isAccusingCustodian = false;
        IsSelectingTarget = true;
        islandPickEligible = new HashSet<ulong>(eligible);

        foreach (ulong id in eligible)
        {
            var island = BoardLayoutManager.Instance != null ? BoardLayoutManager.Instance.GetOpponentIsland(id) : null;
            if (island != null) island.SetIslandGlow(true, new Color(1f, 0.6f, 0.2f, 1f));
        }

        TurnActionUI.Instance?.ShowTemporaryNotification($"<color=orange>{prompt}</color>", GameManager.DecisionSeconds);
        if (allowCancel) TurnActionUI.Instance?.ShowCancelButton();
    }
    public void EndIslandPick()
    {
        if (!isIslandPick) return;
        isIslandPick = false;
        IsSelectingTarget = false;
        islandPickEligible.Clear();
        HighlightOpponentIslands(false);
        TurnActionUI.Instance?.HideCancelButton();
    }

    public void CancelSelection()
    {
        isIslandPick = false;
        IsSelectingTarget = false;
        HighlightOpponentIslands(false);
        if (stealModal != null) stealModal.SetActive(false);
    }

    public void CloseStealModal()
    {
        IsSelectingTarget = false;
        if (stealModal != null) stealModal.SetActive(false);
    }

    private void OnStealCancelled()
    {
        HighlightOpponentIslands(false);
        CloseStealModal();
        IsSelectingTarget = false;
        CancelStealServerRpc();
        if (TurnActionUI.Instance != null)
        {
            TurnActionUI.Instance.ShowActionPrompt();
        }
    }

    public void EnableOpponentSelection()
    {
        IsSelectingTarget = true;
        isAccusingCustodian = false;
        HighlightOpponentIslands(true, new Color(0f, 0.8f, 1f, 1f));
    }

    public void EnableCustodianAccusationSelection()
    {
        IsSelectingTarget = true;
        isAccusingCustodian = true;
        HighlightOpponentIslands(true, new Color(1f, 0.2f, 0.2f, 1f));
    }

    private void HighlightOpponentIslands(bool enable, Color glowColor = default)
    {
        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client.ClientId == NetworkManager.Singleton.LocalClientId) continue;

            var island = BoardLayoutManager.Instance != null ? BoardLayoutManager.Instance.GetOpponentIsland(client.ClientId) : null;
            if (island != null)
            {
                island.SetIslandGlow(enable, glowColor);
            }
        }
    }

    public void OnOpponentIslandClicked(ulong targetId)
    {
        Debug.Log($"[sm] clicked ply #{targetId}");

        if (isIslandPick)
        {
            if (!islandPickEligible.Contains(targetId))
            {
                TurnActionUI.Instance?.ShowTemporaryNotification("<color=red>Pick a different player</color>", 2f);
                return;
            }

            EndIslandPick();
            GameManager.Instance?.SubmitIslandPickServerRpc(targetId);
            return;
        }

        IsSelectingTarget = false;

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

        if (!GameManager.Instance.TryBeginStealTargeting(thiefId, victimId)) return;
        ServerOpenStealModal(thiefId, victimId);
    }
    public void ServerOpenStealModal(ulong thiefId, ulong victimId)
    {
        int victimCardCount = GameManager.Instance.GetPlayerHandCount(victimId);
        string victimName = GameManager.Instance.GetPlayerName(victimId);
        DetectiveCatType victimCat = GameManager.Instance.GetPlayerDetective(victimId);

        if (victimCardCount <= 0) victimCardCount = 3;

        OpenStealModalClientRpc(victimId, victimName, victimCat, victimCardCount, new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { thiefId } }
        });
    }

    [ClientRpc]
    private void OpenStealModalClientRpc(ulong victimId, string victimName, DetectiveCatType victimCat, int cardCount, ClientRpcParams clientRpcParams = default)
    {
        Debug.Log($"<color=green>[sm] received! viuctim ({victimName}), Cards: {cardCount}</color>");

        currentStealTargetId = victimId;
        IsSelectingTarget = false;

        List<int> allIndices = new List<int>();
        for (int i = 0; i < cardCount; i++) allIndices.Add(i);

        OpenFanModal(victimId, victimName, victimCat,
            $"Stealing from <color=yellow>{victimName}</color>",
            allIndices, OnStealCardSelected, true);
    }

    public void ShowPeekModal(ulong victimId, string victimName, DetectiveCatType victimCat,
                              int[] remainingVictimIndices, int pickNumber, int pickTotal)
    {
        currentStealTargetId = victimId;
        IsSelectingTarget = false;

        OpenFanModal(victimId, victimName, victimCat,
            $"Peeking at <color=yellow>{victimName}</color>'s cards ({pickNumber}/{pickTotal})",
            new List<int>(remainingVictimIndices), OnPeekCardSelected, false);
    }

    private void OpenFanModal(ulong victimId, string victimName, DetectiveCatType victimCat, string title, List<int> victimIndices, System.Action<int> onPick, bool allowCancel)
    {
        if (stealModal == null)
        {
            Debug.LogError("[sm] !stealmodal");
            return;
        }

        stealModal.SetActive(true);
        stealModal.transform.SetAsLastSibling();

        if (victimNameText != null) victimNameText.text = title;
        if (stealCancelButton != null) stealCancelButton.gameObject.SetActive(allowCancel);

        if (victimAvatarImage != null && CardDatabase.Instance != null)
        {
            Sprite catSprite = CardDatabase.Instance.GetIslandProfileSprite(victimCat);
            if (catSprite != null)
            {
                victimAvatarImage.sprite = catSprite;
                victimAvatarImage.gameObject.SetActive(true);
            }
        }

        if (cardFanContainer == null || stealCardPrefab == null)
        {
            Debug.LogError("[sm] !fancontiner");
            return;
        }

        foreach (Transform child in cardFanContainer)
        {
            Destroy(child.gameObject);
        }

        int cardCount = victimIndices.Count;
        for (int thiefSlotIndex = 0; thiefSlotIndex < cardCount; thiefSlotIndex++)
        {
            int actualVictimIndex = victimIndices[(cardCount - 1) - thiefSlotIndex];

            GameObject cardObj = Instantiate(stealCardPrefab, cardFanContainer);
            Button btn = cardObj.GetComponentInChildren<Button>();

            RectTransform rt = cardObj.GetComponent<RectTransform>();
            if (rt != null) rt.sizeDelta = new Vector2(130, 190);

            float t = (cardCount <= 1) ? 0f : ((float)thiefSlotIndex / (cardCount - 1) * 2f) - 1f;
            float xPos = t * (cardSpacingX * (cardCount * 0.45f));
            float yPos = -(t * t) * arcDropAmount;
            float zRot = -t * maxTiltAngle;

            var fanVisual = cardObj.GetComponent<StealCardButton>();
            if (fanVisual == null) fanVisual = cardObj.AddComponent<StealCardButton>();
            fanVisual.SetArcPosition(new Vector2(xPos, yPos), Quaternion.Euler(0, 0, zRot));

            int selectedIndex = actualVictimIndex;
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => onPick(selectedIndex));
            }
        }
    }
    public void ShowCounterStealModal(ulong thiefId, string thiefName, DetectiveCatType thiefCat, int cardCount)
    {
        currentStealTargetId = thiefId;
        IsSelectingTarget = false;

        var allIndices = new List<int>();
        for (int i = 0; i < cardCount; i++) allIndices.Add(i);

        OpenFanModal(thiefId, thiefName, thiefCat,
            $"<color=orange>Eye For An Eye!</color> Steal a card back from <color=yellow>{thiefName}</color>",
            allIndices, OnCounterStealSelected, false);
    }

    private void OnCounterStealSelected(int thiefCardIndex)
    {
        Debug.Log($"[sm] counter-steal pick {thiefCardIndex}");
        CloseStealModal();
        GameManager.Instance?.SubmitCounterStealPickServerRpc(thiefCardIndex);
    }
    private void OnPeekCardSelected(int victimCardIndex)
    {
        Debug.Log($"[sm] peek pick {victimCardIndex}");
        CloseStealModal();
        GameManager.Instance?.SubmitBombasticPickServerRpc(victimCardIndex);
    }

    private void OnStealCardSelected(int victimCardIndex)
    {
        Debug.Log($"[sm] thief stole {victimCardIndex}");

        IsSelectingTarget = false;
        HighlightOpponentIslands(false);
        CloseStealModal();

        if (TurnActionUI.Instance != null)
        {
            TurnActionUI.Instance.HideAllActions();
        }

        SubmitStealPickServerRpc(currentStealTargetId, victimCardIndex);
    }
    [Rpc(SendTo.Server)]
    private void CancelStealServerRpc(RpcParams rpcParams = default)
    {
        GameManager.Instance?.OnStealModalCancelled(rpcParams.Receive.SenderClientId);
        //prev used: ReleaseStealTargetLock
    }
    [Rpc(SendTo.Server)]
    private void SubmitStealPickServerRpc(ulong victimId, int cardIndex, RpcParams rpcParams = default)
    {
        ulong thiefId = rpcParams.Receive.SenderClientId;
        GameManager.Instance.ExecuteSteal(thiefId, victimId, cardIndex);
    }
}