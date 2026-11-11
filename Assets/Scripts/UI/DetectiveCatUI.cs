using EvasiveWhisker.Cards;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class DetectiveCatUI : MonoBehaviour
{
    public static DetectiveCatUI Instance { get; private set; }
    [SerializeField] private Image avatarImage;
    [SerializeField] private TMP_Text playerNameText; [SerializeField] private TMP_Text catNameText;
    [SerializeField] private Button profileCardButton; 
    [SerializeField] private Button activateButton;
    [SerializeField] private TMP_Text activateButtonText;

    private bool hasCat;
    private DetectiveCatType cat;
    private int lastUsedRound = 0;
    private string localPlayerName = "Detective";

    private void Awake()
    {
        Instance = this;

        if (activateButton != null) activateButton.onClick.AddListener(OnActivateClicked);
        if (profileCardButton != null) profileCardButton.onClick.AddListener(OnProfileCardClicked);
    }

    public void SetCat(DetectiveCatType catType, string playerName)
    {
        cat = catType;
        hasCat = true;
        lastUsedRound = 0;
        localPlayerName = playerName;

        if (playerNameText != null) playerNameText.text = playerName;
        if (catNameText != null) catNameText.text = DetectiveCatRules.DisplayName(cat);
        if (activateButtonText != null) activateButtonText.text = "ACTIVATE";

        if (avatarImage != null && CardDatabase.Instance != null)
        {
            Sprite sprite = CardDatabase.Instance.GetIslandProfileSprite(cat);
            if (sprite != null) avatarImage.sprite = sprite;
        }
    }
    public void SetLastUsedRound(int round) => lastUsedRound = round;
    private void OnActivateClicked()
    {
        if (!hasCat || GameManager.Instance == null) return;
        int round = GameManager.Instance.CurrentRound.Value;
        string reason = DetectiveCatRules.UnavailableReason(cat, round, lastUsedRound);
        bool isMyTurn = NetworkManager.Singleton.LocalClientId == GameManager.Instance.ActiveTurnPlayerId.Value;

        if (DetectiveCatRules.Timing(cat) == CatTiming.Passive)
        {
            reason = "This cat is passive and triggers automatically.";
        }
        else if (reason == null && isMyTurn)
        {
            if (DetectiveCatRules.Timing(cat) == CatTiming.DrawPhase && GameManager.Instance.CurrentState.Value != GameState.PlayerTurn_Draw)
                reason = "Can only be used during your Draw Phase.";
            else if (DetectiveCatRules.Timing(cat) == CatTiming.ActionPhase && GameManager.Instance.CurrentState.Value != GameState.PlayerTurn_Action)
                reason = "Can only be used during your Action Phase.";
        }
        else if (reason == null && !isMyTurn)
        {
            reason = "It is not your turn.";
        }

        if (reason != null)
        {
            TurnActionUI.Instance?.ShowTemporaryNotification($"<color=red>{reason}</color>", 3f);
            return;
        }
        GameManager.Instance.ActivateDetectiveServerRpc();
    }

    private void OnProfileCardClicked()
    {
        if (OpponentInfoModal.Instance != null && GameManager.Instance != null)
        {
            ulong myId = NetworkManager.Singleton.LocalClientId;
            int myCardCount = PlayerHandController.Instance != null ? PlayerHandController.Instance.HandCount : 0;
            string myActiveAbility = GameManager.Instance.GetPlayerActiveAbilityId(myId);
            OpponentInfoModal.Instance.ShowOpponentInfoModal(myId, localPlayerName, cat, myCardCount, myActiveAbility);
        }
    }
}