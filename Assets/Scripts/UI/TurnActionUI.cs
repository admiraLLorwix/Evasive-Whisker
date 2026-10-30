using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TurnActionUI : MonoBehaviour
{
    public static TurnActionUI Instance { get; private set; }

    [Header("hud")]
    [SerializeField] private TMP_Text instructionText;
    [SerializeField] private TMP_Text roundText;
    [SerializeField] private TMP_Text turnText;

    [Header("action btns")]
    [SerializeField] private GameObject actionChoiceModal;
    [SerializeField] private Button playAbilityChoiceCard;
    [SerializeField] private Button stealChoiceCard;
    [SerializeField] private Button guessCustodianChoiceCard;
    [SerializeField] private Button cancelActionButton;

    private void Awake()
    {
        Instance = this;

        if (playAbilityChoiceCard != null) playAbilityChoiceCard.onClick.AddListener(OnPlayAbilitySelected);
        if (stealChoiceCard != null) stealChoiceCard.onClick.AddListener(OnStealSelected);
        if (guessCustodianChoiceCard != null) guessCustodianChoiceCard.onClick.AddListener(OnGuessCustodianSelected);

        if (cancelActionButton != null) cancelActionButton.onClick.AddListener(OnCancelClicked);

        HideAllActionPanels();
    }

    public void UpdateRoundDisplay(int roundNumber)
    {
        if (roundText != null) roundText.text = $"ROUND {roundNumber} / 10";
    }

    public void ShowDrawPrompt(bool isMyTurn, string activePlayerName)
    {
        HideAllActionPanels();

        if (isMyTurn)
        {
            if (turnText != null) turnText.text = "<color=green>YOUR TURN</color>";
            if (instructionText != null) instructionText.text = "Click the Draw Deck on the Center Island to draw a card";

            DrawDeckClick.Instance?.SetGlow(true);
        }
        else
        {
            if (turnText != null) turnText.text = $"{activePlayerName}'s Turn";
            if (instructionText != null) instructionText.text = $"Waiting for {activePlayerName} to draw...";
            DrawDeckClick.Instance?.SetGlow(false);
        }
    }

    public void ShowDiscardPrompt(int currentCount, int maxLimit)
    {
        HideAllActionPanels();
        DrawDeckClick.Instance?.SetGlow(false);

        if (turnText != null)
            turnText.text = "<color=red><b>DISCARD REQUIRED</b></color>";

        if (instructionText != null)
            instructionText.text = $"<color=red>HAND LIMIT EXCEEDED ({currentCount}/{maxLimit}) DRAG A CARD UP TO DISCARD IT</color>";
    }

    public void ShowActionPhaseModal()
    {
        DrawDeckClick.Instance?.SetGlow(false);

        if (turnText != null) turnText.text = "<color=green>YOUR TURN</color>";
        if (instructionText != null) instructionText.text = "Choose your action for this turn";

        if (actionChoiceModal != null) actionChoiceModal.SetActive(true);
        if (cancelActionButton != null) cancelActionButton.gameObject.SetActive(false);

        int custodyCount = PlayerHandController.Instance != null ? PlayerHandController.Instance.GetCustodyCardCount() : 0;
        bool hasAbilities = PlayerHandController.Instance != null && PlayerHandController.Instance.HasAbilityCards();

        if (playAbilityChoiceCard != null) playAbilityChoiceCard.interactable = hasAbilities;
        if (stealChoiceCard != null) stealChoiceCard.interactable = true;
        if (guessCustodianChoiceCard != null) guessCustodianChoiceCard.interactable = (custodyCount >= 3);
    }

    private void OnPlayAbilitySelected()
    {
        if (actionChoiceModal != null) actionChoiceModal.SetActive(false);
        if (cancelActionButton != null) cancelActionButton.gameObject.SetActive(true);
        if (instructionText != null) instructionText.text = "Drag an Ability Card or Detective Cat up to play it";

        PlayerHandController.Instance?.SetAbilityCardsPlayable(true);
    }

    private void OnStealSelected()
    {
        if (actionChoiceModal != null) actionChoiceModal.SetActive(false);
        if (cancelActionButton != null) cancelActionButton.gameObject.SetActive(true);
        if (instructionText != null) instructionText.text = "Click an Opponent's Island to steal from";

        StealManager.Instance?.EnableOpponentSelection();
    }

    private void OnGuessCustodianSelected()
    {
        if (actionChoiceModal != null) actionChoiceModal.SetActive(false);
        if (cancelActionButton != null) cancelActionButton.gameObject.SetActive(true);
        if (instructionText != null) instructionText.text = "Click the Opponent you accuse of holding the WhiskerBlower";

        StealManager.Instance?.EnableCustodianAccusationSelection();
    }

    private void OnCancelClicked()
    {
        PlayerHandController.Instance?.SetAbilityCardsPlayable(false);
        StealManager.Instance?.CancelSelection();
        ShowActionPhaseModal();
    }

    public void HideAllActionPanels()
    {
        if (actionChoiceModal != null) actionChoiceModal.SetActive(false);
        if (cancelActionButton != null) cancelActionButton.gameObject.SetActive(false);
    }

    public void ShowActionPrompt() => ShowActionPhaseModal();
    public void HideAllActions() => HideAllActionPanels();
}