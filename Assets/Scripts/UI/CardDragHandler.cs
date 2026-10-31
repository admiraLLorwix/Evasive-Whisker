using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using EvasiveWhisker.Cards;

public class CardDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public NetworkCard CardData { get; private set; }
    public bool IsDraggable { get; set; } = false;

    private RectTransform rectTransform;
    private Canvas canvas;
    private CanvasGroup canvasGroup;
    private Vector2 originalPosition;
    private Transform originalParent;
    private int originalSiblingIndex;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvas = GetComponentInParent<Canvas>();
        canvasGroup = gameObject.GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    public void Initialize(NetworkCard card)
    {
        CardData = card;
        IsDraggable = (card.Type == CardType.Ability);
    }

    public void SetHighlighted(bool highlighted)
    {
        Outline outline = GetComponentInChildren<Outline>();
        if (outline == null)
        {
            var img = GetComponentInChildren<Image>();
            if (img != null) outline = img.gameObject.AddComponent<Outline>();
        }

        if (outline != null)
        {
            outline.effectDistance = new Vector2(5f, -5f);
            outline.effectColor = new Color(1f, 0.85f, 0.2f, 1f);
            outline.enabled = highlighted;
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        bool isMyTurn = GameManager.Instance != null &&
                        GameManager.Instance.ActiveTurnPlayerId.Value == Unity.Netcode.NetworkManager.Singleton.LocalClientId;

        bool canDragInDiscard = (GameManager.Instance?.CurrentState.Value == GameState.PlayerTurn_DiscardPrompt);
        bool canDragInAction = (GameManager.Instance?.CurrentState.Value == GameState.PlayerTurn_Action && CardData.Type == CardType.Ability);

        if (!isMyTurn || (!canDragInDiscard && !canDragInAction))
        {
            eventData.pointerDrag = null;
            return;
        }

        originalPosition = rectTransform.anchoredPosition;
        originalParent = transform.parent;
        originalSiblingIndex = transform.GetSiblingIndex();

        transform.SetParent(canvas.transform, true);
        canvasGroup.blocksRaycasts = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (canvas != null)
        {
            rectTransform.anchoredPosition += eventData.delta / canvas.scaleFactor;
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        canvasGroup.blocksRaycasts = true;

        bool isMyTurn = GameManager.Instance != null &&
                        GameManager.Instance.ActiveTurnPlayerId.Value == Unity.Netcode.NetworkManager.Singleton.LocalClientId;

        float dragUpThreshold = Screen.height * 0.35f;

        if (isMyTurn && transform.position.y > dragUpThreshold)
        {
            if (GameManager.Instance?.CurrentState.Value == GameState.PlayerTurn_DiscardPrompt)
            {
                if (CardData.Type == CardType.WhiskerBlower)
                {
                    ReturnToHand();
                    return;
                }

                ReturnToHand();
                PlayerHandController.Instance?.PromptDiscardConfirmation(CardData);
                return;
            }

            if (GameManager.Instance?.CurrentState.Value == GameState.PlayerTurn_Action && CardData.Type == CardType.Ability)
            {
                Debug.Log($"<color=green>[Action] played: {CardData.CardId}</color>");

                gameObject.SetActive(false);

                PlayerHandController.Instance?.RemoveCardObject(gameObject, CardData);

                TurnActionUI.Instance?.HideAllActions();
                GameManager.Instance?.SubmitPlayAbilityCardServerRpc(CardData);

                return;
            }
        }

        ReturnToHand();
    }

    private void ReturnToHand()
    {
        if (originalParent != null)
        {
            transform.SetParent(originalParent, true);
            transform.SetSiblingIndex(originalSiblingIndex);
            rectTransform.anchoredPosition = originalPosition;
            PlayerHandController.Instance?.UpdateHandFanLayout();
        }
    }
}