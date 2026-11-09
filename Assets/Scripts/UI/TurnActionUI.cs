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

    [SerializeField] private Button endTurnButton;
    [SerializeField] private GameObject globalDimPanel;
    private bool lastAbilityOnly = false;
    private bool lastAllowSkip = false;

    private void Awake()
    {
        Instance = this;

        if (playAbilityChoiceCard != null) playAbilityChoiceCard.onClick.AddListener(OnPlayAbilitySelected);
        if (stealChoiceCard != null) stealChoiceCard.onClick.AddListener(OnStealSelected);
        if (guessCustodianChoiceCard != null) guessCustodianChoiceCard.onClick.AddListener(OnGuessCustodianSelected);

        if (cancelActionButton != null) cancelActionButton.onClick.AddListener(OnCancelClicked);
        if (endTurnButton != null) endTurnButton.onClick.AddListener(OnEndTurnClicked);   // [M2a]

        HideAllActionPanels();
    }

    public void UpdateRoundDisplay(int roundNumber)
    {
        if (roundText != null) roundText.text = $"ROUND {roundNumber} / 10";
    }
    public void SetInstructionText(string instruction, string turnLabel = null)
    {
        if (instructionText != null) instructionText.text = instruction;
        if (turnLabel != null && turnText != null) turnText.text = turnLabel;
    }
    public void ShowTemporaryNotification(string message, float duration = 3.5f)
    {
        StartCoroutine(TempNoticeRoutine(message, duration));
    }

    private System.Collections.IEnumerator TempNoticeRoutine(string message, float duration)
    {
        string prevText = instructionText != null ? instructionText.text : "";
        if (instructionText != null) instructionText.text = message;
        yield return new WaitForSeconds(duration);
        if (instructionText != null && instructionText.text == message) instructionText.text = prevText;
    }

    public void ShowDrawPrompt(bool isMyTurn, string activePlayerName)
    {
        HideAllActionPanels();

        if (isMyTurn)
        {
            if (turnText != null) turnText.text = "<color=green>YOUR TURN</color>";
            if (instructionText != null) instructionText.text = "Click the Draw Deck on the Center Island to draw a card";
            UIFocusManager.Instance?.FocusOn(FocusTarget.DrawDeck);
            DrawDeckClick.Instance?.SetGlow(true);
        }
        else
        {
            if (turnText != null) turnText.text = $"{activePlayerName}'s Turn";
            if (instructionText != null) instructionText.text = $"Waiting for {activePlayerName} to draw...";
            UIFocusManager.Instance?.ClearFocus();
            DrawDeckClick.Instance?.SetGlow(false); 
        }
    }

    public void ShowDiscardPrompt(int currentCount, int maxLimit)
    {
        HideAllActionPanels();
        DrawDeckClick.Instance?.SetGlow(false);
        UIFocusManager.Instance?.FocusOn(FocusTarget.Hand);

        if (turnText != null)
            turnText.text = "<color=red><b>DISCARD REQUIRED</b></color>";

        if (instructionText != null)
            instructionText.text = $"<color=red>HAND LIMIT EXCEEDED ({currentCount}/{maxLimit}) DRAG A CARD UP TO DISCARD IT</color>";
    }

    public void ShowActionPhaseModal(bool abilityOnly = false, bool allowSkip = false)
    {
        lastAbilityOnly = abilityOnly;
        lastAllowSkip = allowSkip;

        DrawDeckClick.Instance?.SetGlow(false);

        if (turnText != null) turnText.text = "<color=green>YOUR TURN</color>";
        if (instructionText != null)
        {
            instructionText.text = abilityOnly ? "Second action: you can only play an Ability now"
                                 : allowSkip ? "Second action: choose again, or end your turn"
                                 : "Choose your action for this turn";
        }

        if (actionChoiceModal != null) actionChoiceModal.SetActive(true);
        if (cancelActionButton != null) cancelActionButton.gameObject.SetActive(false);
        if (endTurnButton != null) endTurnButton.gameObject.SetActive(allowSkip);

        int custodyCount = PlayerHandController.Instance != null ? PlayerHandController.Instance.GetCustodyCardCount() : 0;
        bool hasAbilities = PlayerHandController.Instance != null && PlayerHandController.Instance.HasPlayableAbilityCards();
        if (playAbilityChoiceCard != null) playAbilityChoiceCard.interactable = hasAbilities;
        if (stealChoiceCard != null) stealChoiceCard.interactable = !abilityOnly;
        if (guessCustodianChoiceCard != null) guessCustodianChoiceCard.interactable = (custodyCount >= 3) && !abilityOnly;
    }
    private void ReportStep(GameManager.DecisionStep step) =>
    GameManager.Instance?.ReportDecisionStepServerRpc((int)step, "");

    private void OnPlayAbilitySelected()
    {
        ReportStep(GameManager.DecisionStep.PickAbility);
        if (actionChoiceModal != null) actionChoiceModal.SetActive(false);
        if (cancelActionButton != null) cancelActionButton.gameObject.SetActive(true);
        if (instructionText != null) instructionText.text = "Drag an Ability Card or Detective Cat up to play it";

        PlayerHandController.Instance?.SetAbilityCardsPlayable(true);
    }

    private void OnStealSelected()
    {
        ReportStep(GameManager.DecisionStep.StealOpponent);
        if (actionChoiceModal != null) actionChoiceModal.SetActive(false);
        if (cancelActionButton != null) cancelActionButton.gameObject.SetActive(true);
        if (instructionText != null) instructionText.text = "Click an Opponent's Island to steal from";

        StealManager.Instance?.EnableOpponentSelection();
    }

    private void OnGuessCustodianSelected()
    {
        ReportStep(GameManager.DecisionStep.AccuseTarget);
        if (actionChoiceModal != null) actionChoiceModal.SetActive(false);
        if (cancelActionButton != null) cancelActionButton.gameObject.SetActive(true);
        if (instructionText != null) instructionText.text = "Click the Opponent you accuse of holding the WhiskerBlower";

        StealManager.Instance?.EnableCustodianAccusationSelection();
    }

    private void OnCancelClicked()
    {
        if (TargetingManager.Instance != null && TargetingManager.Instance.IsTargeting)
        {
            TargetingManager.Instance.CancelTargeting();
        }

        PlayerHandController.Instance?.SetAbilityCardsPlayable(false);
        StealManager.Instance?.CancelSelection();
        ShowActionPrompt();
        ReportStep(GameManager.DecisionStep.ChooseAction);
    }

    private void OnEndTurnClicked()
    {
        HideAllActionPanels();
        PlayerHandController.Instance?.SetAbilityCardsPlayable(false);
        GameManager.Instance?.SubmitEndTurnServerRpc();
    }
    public void ShowCancelButton()
    {
        if (actionChoiceModal != null) actionChoiceModal.SetActive(false);
        if (cancelActionButton != null) cancelActionButton.gameObject.SetActive(true);
    }

    public void HideCancelButton()
    {
        if (cancelActionButton != null) cancelActionButton.gameObject.SetActive(false);
    }

    public void HideAllActionPanels()
    {
        if (actionChoiceModal != null) actionChoiceModal.SetActive(false);
        if (cancelActionButton != null) cancelActionButton.gameObject.SetActive(false);
        if (endTurnButton != null) endTurnButton.gameObject.SetActive(false);
        UIFocusManager.Instance?.ClearFocus();
    }

    public void ShowActionPrompt() => ShowActionPhaseModal(lastAbilityOnly, lastAllowSkip);
    public void HideAllActions() => HideAllActionPanels();
}