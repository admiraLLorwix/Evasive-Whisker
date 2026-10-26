using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TurnActionUI : MonoBehaviour
{
    public static TurnActionUI Instance { get; private set; }

    [Header("turn visuals")]
    [SerializeField] private TMP_Text turnStatusText;
    [SerializeField] private TMP_Text instructionText;
    [SerializeField] private TMP_Text roundText;

    [Header("center island")]
    [SerializeField] private Button drawDeckButton;
    //discard pile deck

    [Header("action phase btns")]
    [SerializeField] private GameObject actionButtonsContainer;
    [SerializeField] private Button playAbilityButton;
    [SerializeField] private Button stealButton;
    [SerializeField] private Button guessCustodianButton;
    [SerializeField] private Button cancelActionButton;

    private void Awake()
    {
        Instance = this;

        if (drawDeckButton != null)
        {
            drawDeckButton.onClick.AddListener(OnDrawDeckClicked);
            drawDeckButton.interactable = false;
        }

        if (playAbilityButton != null) playAbilityButton.onClick.AddListener(OnPlayAbilityClicked);
        if (stealButton != null) stealButton.onClick.AddListener(OnStealClicked);
        if (guessCustodianButton != null) guessCustodianButton.onClick.AddListener(OnGuessCustodianClicked);

        if (cancelActionButton != null) cancelActionButton.onClick.AddListener(OnCancelActionClicked);

        HideAllActions();
    }

    private void OnDrawDeckClicked()
    {
        if (drawDeckButton != null) drawDeckButton.interactable = false;
        GameManager.Instance.RequestDrawCardServerRpc();
    }

    public void UpdateRoundDisplay(int roundNumber)
    {
        if (roundText != null) roundText.text = $"ROUND {roundNumber} / 10";
    }

    public void ShowDrawPrompt(bool isMyTurn, string activePlayerName)
    {
        HideAllActions();
        if (isMyTurn)
        {
            if (turnStatusText != null) turnStatusText.text = "<color=green>YOUR TURN</color>";
            if (instructionText != null) instructionText.text = "Click the Draw Deck on the Center Island to draw a card";
            if (drawDeckButton != null) drawDeckButton.interactable = true;
        }
        else
        {
            if (turnStatusText != null) turnStatusText.text = $"{activePlayerName}'s Turn";
            if (instructionText != null) instructionText.text = $"Waiting for {activePlayerName} to draw...";
            if (drawDeckButton != null) drawDeckButton.interactable = false;
        }
    }

    public void ShowDiscardPrompt(int currentCount, int maxLimit)
    {
        HideAllActions();
        if (turnStatusText != null) turnStatusText.text = "<color=red>HAND LIMIT EXCEEDED</color>";
        if (instructionText != null) instructionText.text = $"You have {currentCount}/{maxLimit} cards, Click a card in your hand to DISCARD it";
    }

    public void ShowActionPrompt()
    {
        if (turnStatusText != null) turnStatusText.text = "<color=green>ACTION PHASE</color>";
        if (instructionText != null) instructionText.text = "Choose an Action: Play Ability or Steal from an Opponent";

        if (drawDeckButton != null) drawDeckButton.interactable = false;
        if (actionButtonsContainer != null) actionButtonsContainer.SetActive(true);
        if (cancelActionButton != null) cancelActionButton.gameObject.SetActive(false);

        int custodyCount = PlayerHandController.Instance != null ? PlayerHandController.Instance.GetCustodyCardCount() : 0;
        bool hasAbilities = PlayerHandController.Instance != null && PlayerHandController.Instance.HasAbilityCards();

        if (playAbilityButton != null) playAbilityButton.interactable = hasAbilities;
        if (stealButton != null) stealButton.interactable = true;
        if (guessCustodianButton != null) guessCustodianButton.interactable = (custodyCount >= 3);
    }

    private void OnPlayAbilityClicked()
    {
        actionButtonsContainer.SetActive(false);
        if (cancelActionButton != null) cancelActionButton.gameObject.SetActive(true);
        if (instructionText != null) instructionText.text = "<color=yellow>Drag an Ability Card to play it</color>";

        if (PlayerHandController.Instance != null)
        {
            PlayerHandController.Instance.SetAbilityCardsPlayable(true);
        }
    }

    private void OnStealClicked()
    {
        actionButtonsContainer.SetActive(false);
        if (cancelActionButton != null) cancelActionButton.gameObject.SetActive(true);
        if (instructionText != null) instructionText.text = "<color=yellow>Click an Opponent's Island to steal a card from</color>";

        if (StealManager.Instance != null)
        {
            StealManager.Instance.EnableOpponentSelection();
        }
    }

    private void OnGuessCustodianClicked()
    {
        actionButtonsContainer.SetActive(false);
        if (cancelActionButton != null) cancelActionButton.gameObject.SetActive(true);
        if (instructionText != null) instructionText.text = "<color=yellow>Click the Opponent you accuse of holding the WhiskerBlower</color>";

        if (StealManager.Instance != null)
        {
            StealManager.Instance.EnableCustodianAccusationSelection();
        }
    }

    private void OnCancelActionClicked()
    {
        if (PlayerHandController.Instance != null)
        {
            PlayerHandController.Instance.SetAbilityCardsPlayable(false);
        }

        if (StealManager.Instance != null)
        {
            StealManager.Instance.CancelSelection();
        }

        ShowActionPrompt();
    }

    public void HideAllActions()
    {
        if (drawDeckButton != null) drawDeckButton.interactable = false;
        if (actionButtonsContainer != null) actionButtonsContainer.SetActive(false);
        if (cancelActionButton != null) cancelActionButton.gameObject.SetActive(false);
    }
}