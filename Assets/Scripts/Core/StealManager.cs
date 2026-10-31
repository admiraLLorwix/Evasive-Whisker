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
        CloseStealModal();
        IsSelectingTarget = false;
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

        if (stealModal == null)
        {
            Debug.LogError("[sm] !stealmodal");
            return;
        }

        stealModal.SetActive(true);
        stealModal.transform.SetAsLastSibling();

        if (victimNameText != null) victimNameText.text = $"Stealing from <color=yellow>{victimName}</color>";

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

        for (int thiefSlotIndex = 0; thiefSlotIndex < cardCount; thiefSlotIndex++)
        {
            int actualVictimIndex = (cardCount - 1) - thiefSlotIndex;

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
                btn.onClick.AddListener(() => OnStealCardSelected(selectedIndex));
            }
        }
    }

    private void OnStealCardSelected(int victimCardIndex)
    {
        IsSelectingTarget = false;
        CloseStealModal();
        SubmitStealPickServerRpc(currentStealTargetId, victimCardIndex);
    }

    [Rpc(SendTo.Server)]
    private void SubmitStealPickServerRpc(ulong victimId, int cardIndex, RpcParams rpcParams = default)
    {
        ulong thiefId = rpcParams.Receive.SenderClientId;
        GameManager.Instance.ExecuteSteal(thiefId, victimId, cardIndex);
    }

}