using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TurnActionUI : MonoBehaviour
{
    public static TurnActionUI Instance { get; private set; }

    [Header("hud")]
    [SerializeField] private TMP_Text instructionText;

    [Header("hud buttons")]
    [SerializeField] private Button accuseButton;
    [SerializeField] private Button cancelActionButton;
    [SerializeField] private Button endTurnButton;

    private void Awake()
    {
        Instance = this;

        if (accuseButton != null) accuseButton.onClick.AddListener(OnAccuseClicked);

        if (cancelActionButton != null) cancelActionButton.onClick.AddListener(OnCancelClicked);
        if (endTurnButton != null) endTurnButton.onClick.AddListener(OnEndTurnClicked);   // [M2a]

        HideAllActionPanels();
    }

    public void SetInstructionText(string instruction, string turnLabel = null)
    {
        if (instructionText != null) instructionText.text = instruction;
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
        DrawDeckClick.Instance?.SetGlow(isMyTurn);

        if (isMyTurn)
        {
            SetInstructionText("Click the Draw Deck on the Center Island to draw a card");
            UIFocusManager.Instance?.FocusOn(FocusTarget.DrawDeck);
        }
        else
        {
            SetInstructionText($"Waiting for {activePlayerName} to draw...");
            UIFocusManager.Instance?.ClearFocus();
        }
    }

    public void ShowDiscardPrompt(int currentCount, int maxLimit)
    {
        HideAllActionPanels();
        DrawDeckClick.Instance?.SetGlow(false);
        UIFocusManager.Instance?.FocusOn(FocusTarget.Hand);

        SetInstructionText($"<color=red>HAND LIMIT EXCEEDED ({currentCount}/{maxLimit}) DRAG A CARD UP TO DISCARD IT</color>");
    }

    public void ShowActionPhase(bool isMyTurn, bool canAccuse, bool allowEndTurn)
    {
        DrawDeckClick.Instance?.SetGlow(false);

        if (!isMyTurn)
        {
            HideAllActionPanels();
            SetInstructionText("Waiting for opponents...");
            return;
        }
        SetInstructionText("Drag an Ability Card up to play, or click an opponent to view them");

        PlayerHandController.Instance?.SetAbilityCardsPlayable(true);
        UIFocusManager.Instance?.ClearFocus();

        StealManager.Instance?.SetActionPhaseHighlights(true);

        if (accuseButton != null) accuseButton.gameObject.SetActive(canAccuse);
        if (cancelActionButton != null) cancelActionButton.gameObject.SetActive(false);
        if (endTurnButton != null) endTurnButton.gameObject.SetActive(allowEndTurn);
    }
    public void ShowActionPrompt()
    {
        bool canAccuse = GameManager.Instance != null && PlayerHandController.Instance.GetCustodyCardCount() >= 3;
        ShowActionPhase(true, canAccuse, false);
    }

    public void ShowCancelButton()
    {
        if (cancelActionButton != null) cancelActionButton.gameObject.SetActive(true);
    }

    public void HideCancelButton()
    {
        if (cancelActionButton != null) cancelActionButton.gameObject.SetActive(false);
    }



    private void OnAccuseClicked()
    {
        if (PlayerHandController.Instance != null && PlayerHandController.Instance.GetCustodyCardCount() < 3)
        {
            ShowTemporaryNotification("<color=red>You need 3 Custody cards to accuse someone!</color>", 3f);
            return;
        }
        ReportStep(GameManager.DecisionStep.AccuseTarget);
        if (accuseButton != null) accuseButton.gameObject.SetActive(false);
        if (cancelActionButton != null) cancelActionButton.gameObject.SetActive(true);
        SetInstructionText("Click the Opponent you accuse of holding the WhiskerBlower");
        PlayerHandController.Instance?.SetAbilityCardsPlayable(false);

        StealManager.Instance?.EnableCustodianAccusationSelection();
    }

    public void OnCardDraggedUp()
    {
        if (cancelActionButton != null) cancelActionButton.gameObject.SetActive(true);
        if (accuseButton != null) accuseButton.gameObject.SetActive(false);
    }

    private void OnCancelClicked()
    {
        TargetingManager.Instance?.CancelTargeting();

        StealManager.Instance?.CancelSelection();

        if (cancelActionButton != null) cancelActionButton.gameObject.SetActive(false);
        if (accuseButton != null) accuseButton.gameObject.SetActive(GameManager.Instance != null && PlayerHandController.Instance.GetCustodyCardCount() >= 3);

        SetInstructionText("Drag an Ability Card up to play, or click an opponent to view them");
        PlayerHandController.Instance?.SetAbilityCardsPlayable(true);
        UIFocusManager.Instance?.FocusOn(FocusTarget.Hand);

        //ReportStep(GameManager.DecisionStep.ChooseAction);
    }

    private void OnEndTurnClicked()
    {
        HideAllActionPanels();
        PlayerHandController.Instance?.SetAbilityCardsPlayable(false);
        GameManager.Instance?.SubmitEndTurnServerRpc();
    }
    private void ReportStep(GameManager.DecisionStep step) =>
    GameManager.Instance?.ReportDecisionStepServerRpc((int)step, "");

    public void HideAllActionPanels()
    {
        if (accuseButton != null) accuseButton.gameObject.SetActive(false);
        if (cancelActionButton != null) cancelActionButton.gameObject.SetActive(false);
        if (endTurnButton != null) endTurnButton.gameObject.SetActive(false);
        StealManager.Instance?.SetActionPhaseHighlights(false);
        UIFocusManager.Instance?.ClearFocus();
    }

    public void HideAllActions() => HideAllActionPanels();
}