using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;
using EvasiveWhisker.Cards;

public class RoundStatusUI : MonoBehaviour
{
    public static RoundStatusUI Instance { get; private set; }
    [SerializeField] private TMP_Text roundText;
    [SerializeField] private Image playerAvatar; [SerializeField] private TMP_Text playerNameText;
    [SerializeField] private TMP_Text phaseText; [SerializeField] private TMP_Text timerText;
    [SerializeField] private Image timerFillImage;
    [SerializeField] private Color timerNormalColor = Color.green;
    [SerializeField] private Color timerUrgentColor = Color.red;

    private ulong activePlayerId;
    private Coroutine timerRoutine;

    private void Awake()
    {
        Instance = this;
        HideTimer();
    }
    public void UpdateTurnUI(ulong activeId, string playerName, int roundNumber)
    {
        activePlayerId = activeId;

        if (roundText != null) roundText.text = $"ROUND {roundNumber} / 10";
        if (playerNameText != null)
        {
            playerNameText.text = playerName;
            bool isLocal = NetworkManager.Singleton.LocalClientId == activeId;
            playerNameText.color = isLocal ? new Color(0.15f, 0.5f, 0.2f) : Color.black;
        }

        if (playerAvatar != null && CardDatabase.Instance != null && GameManager.Instance != null)
        {
            DetectiveCatType cat = GameManager.Instance.GetPlayerDetective(activeId);
            Sprite sprite = CardDatabase.Instance.GetIslandProfileSprite(cat);
            if (sprite != null)
            {
                playerAvatar.sprite = sprite;
                playerAvatar.gameObject.SetActive(true);
            }
            else
            {
                playerAvatar.gameObject.SetActive(false);
            }
        }
    }

    private void Update()
    {
        if (phaseText == null || GameManager.Instance == null) return;

        GameState state = GameManager.Instance.CurrentState.Value;

        switch (state)
        {
            case GameState.PlayerTurn_Draw:
                phaseText.text = "Drawing...";
                break;
            case GameState.PlayerTurn_Action:
                phaseText.text = "Choosing Action...";
                break;
            case GameState.PlayerTurn_DiscardPrompt:
                phaseText.text = "<color=red>Discarding...</color>";
                break;
            case GameState.PlayerTurn_Resolving:
                phaseText.text = "Resolving...";
                break;
            case GameState.GameOver:
                phaseText.text = "Game Over";
                break;
            default:
                phaseText.text = "Waiting...";
                break;
        }
    }
    public void ShowTimer(float seconds)
    {
        if (timerRoutine != null) StopCoroutine(timerRoutine);
        timerRoutine = StartCoroutine(CountdownRoutine(seconds));
    }

    public void HideTimer()
    {
        if (timerRoutine != null) StopCoroutine(timerRoutine);
        timerRoutine = null;
        if (timerText != null) timerText.gameObject.SetActive(false);
        if (timerFillImage != null) timerFillImage.gameObject.SetActive(false);
    }

    private IEnumerator CountdownRoutine(float totalSeconds)
    {
        if (timerText != null) timerText.gameObject.SetActive(true);
        if (timerFillImage != null) timerFillImage.gameObject.SetActive(true);

        float endTime = Time.unscaledTime + totalSeconds;

        while (Time.unscaledTime < endTime)
        {
            float remaining = endTime - Time.unscaledTime;

            if (timerText != null)
                timerText.text = Mathf.CeilToInt(remaining).ToString();
            if (timerFillImage != null)
            {
                float fill = remaining / totalSeconds;
                timerFillImage.fillAmount = fill;
                timerFillImage.color = remaining <= 3f ? timerUrgentColor : timerNormalColor;
                if (timerText != null) timerText.color = remaining <= 3f ? timerUrgentColor : Color.black;
            }

            yield return null;
        }

        HideTimer();
    }
}