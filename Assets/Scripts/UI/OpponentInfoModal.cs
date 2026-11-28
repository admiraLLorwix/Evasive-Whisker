using UnityEngine;
using UnityEngine.UI;
using TMPro;
using EvasiveWhisker.Cards;
using Unity.Netcode;
using System.Collections;

public class OpponentInfoModal : MonoBehaviour
{
    public static OpponentInfoModal Instance { get; private set; }

    [SerializeField] private GameObject modalRoot;
    [SerializeField] private Button backgroundDimButton; [SerializeField] private Button closeButton;
    [SerializeField] private TMP_Text playerNameText; [SerializeField] private TMP_Text cardCountText;
    [SerializeField] private Image detectiveCatImage; [SerializeField] private Image peekingCatAvatar;
    [SerializeField] private Image activeAbilityImage;
    [SerializeField] private Button stealButton;
    private ulong currentOpponentId;
    private bool canStealFromTarget;
    private Coroutine shakeRoutine;
    private Vector2 stealBtnOriginalPos;
    private Color stealBtnOriginalColor;


    private void Awake()
    {
        Instance = this;
        Hide();
        if (closeButton != null) closeButton.onClick.AddListener(Hide);
        if (backgroundDimButton != null) backgroundDimButton.onClick.AddListener(Hide);

        if (stealButton != null)
        {
            stealButton.onClick.AddListener(OnStealClicked);
            stealBtnOriginalPos = stealButton.transform.localPosition;
            if (stealButton.targetGraphic != null)
                stealBtnOriginalColor = stealButton.targetGraphic.color;
        }
    }

    public void ShowOpponentInfoModal(ulong opponentId, string directName = "", DetectiveCatType catType = DetectiveCatType.HoarderMolly, int cardCount = 3, string directAbilityId = null)
    {
        currentOpponentId = opponentId;
        gameObject.SetActive(true);
        if (modalRoot != null) modalRoot.SetActive(true);

        Transform panel = transform.Find("InfoCardPanel");
        if (panel != null) panel.gameObject.SetActive(true);

        Transform dim = transform.Find("DimBackground");
        if (dim != null) dim.gameObject.SetActive(true);

        transform.SetAsLastSibling();

        string pName = !string.IsNullOrEmpty(directName) ? directName : $"Detective #{opponentId}";

        if (playerNameText != null) playerNameText.text = pName;
        if (cardCountText != null) cardCountText.text = $"{cardCount}";

        if (peekingCatAvatar != null && CardDatabase.Instance != null)
        {
            Sprite avatarSprite = CardDatabase.Instance.GetIslandProfileSprite(catType);
            if (avatarSprite != null)
            {
                peekingCatAvatar.sprite = avatarSprite;
                peekingCatAvatar.gameObject.SetActive(true);
            }
        }

        if (detectiveCatImage != null && CardDatabase.Instance != null)
        {
            Sprite catCardSprite = CardDatabase.Instance.GetLocalHandCardSprite(catType);
            if (catCardSprite != null)
            {
                detectiveCatImage.sprite = catCardSprite;
                detectiveCatImage.gameObject.SetActive(true);
            }
        }

        string activeAbilityId = !string.IsNullOrEmpty(directAbilityId) ? directAbilityId :
            (GameManager.Instance != null ? GameManager.Instance.GetPlayerActiveAbilityId(opponentId) : null);

        bool abilityShown = false;

        if (!string.IsNullOrEmpty(activeAbilityId) && CardDatabase.Instance != null)
        {
            Sprite abilitySprite = CardDatabase.Instance.GetCardSprite(activeAbilityId);
            if (abilitySprite != null && activeAbilityImage != null)
            {
                activeAbilityImage.sprite = abilitySprite;
                activeAbilityImage.gameObject.SetActive(true);
                abilityShown = true;
            }
        }
        if (!abilityShown && activeAbilityImage != null)
        {
            activeAbilityImage.gameObject.SetActive(false);
            activeAbilityImage.sprite = null;
        }

        canStealFromTarget = false;
        if (GameManager.Instance != null && NetworkManager.Singleton != null)
        {
            ulong localId = NetworkManager.Singleton.LocalClientId;
            bool isMyTurn = GameManager.Instance.ActiveTurnPlayerId.Value == localId;
            bool isActionPhase = GameManager.Instance.CurrentState.Value == GameState.PlayerTurn_Action;

            if (isMyTurn && isActionPhase && opponentId != localId && cardCount > 0)
            {
                canStealFromTarget = true;
            }
        }

        if (stealButton != null)
        {
            stealButton.gameObject.SetActive(true);
            stealButton.interactable = true;

            stealButton.transform.localPosition = stealBtnOriginalPos;
            if (stealButton.targetGraphic != null)
                stealButton.targetGraphic.color = stealBtnOriginalColor;
        }
    }

    private void OnStealClicked()
    {
        if (!canStealFromTarget)
        {
            string reason = "You can't steal right now";
            if (GameManager.Instance != null && NetworkManager.Singleton != null)
            {
                ulong localId = NetworkManager.Singleton.LocalClientId;
                if (GameManager.Instance.ActiveTurnPlayerId.Value != localId)
                    reason = "It's not your turn!";
                else if (GameManager.Instance.CurrentState.Value != GameState.PlayerTurn_Action)
                    reason = "Not in action phase!";
                else if (currentOpponentId == localId)
                    reason = "You can't steal from yourself!";
                else if (GameManager.Instance.GetPlayerHandCount(currentOpponentId) <= 0)
                    reason = "They have no cards to steal!";
            }
            if (shakeRoutine != null) StopCoroutine(shakeRoutine);
            shakeRoutine = StartCoroutine(ShakeStealButton(reason));
            return;
        }
        Hide();
        PlayerHandController.Instance?.SetAbilityCardsPlayable(false);
        TurnActionUI.Instance?.HideAllActions();
        if (StealManager.Instance != null)
        {
            StealManager.Instance.RequestStealFromModal(currentOpponentId);
        }
    }


    public void Hide()
    {
        if (modalRoot != null) modalRoot.SetActive(false);

        Transform panel = transform.Find("InfoCardPanel");
        if (panel != null) panel.gameObject.SetActive(false);

        Transform dim = transform.Find("DimBackground");
        if (dim != null) dim.gameObject.SetActive(false);

        gameObject.SetActive(false);
    }


    private IEnumerator ShakeStealButton(string message)
    {
        if (stealButton == null) yield break;

        RectTransform rt = stealButton.transform as RectTransform;
        Graphic g = stealButton.targetGraphic;

        float duration = 0.3f;
        float elapsed = 0f;
        float strength = 9f;

        if (g != null) g.color = new Color(1f, 0.3f, 0.3f, 1f);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float fade = 1f - Mathf.Clamp01(elapsed / duration);
            float offset = Mathf.Sin(elapsed * 70f) * strength * fade;

            rt.localPosition = stealBtnOriginalPos + new Vector2(offset, 0f);
            yield return null;
        }

        rt.localPosition = stealBtnOriginalPos;
        if (g != null) g.color = stealBtnOriginalColor;

        TurnActionUI.Instance?.ShowTemporaryNotification($"<color=red>{message}</color>", 3f);
    }
}