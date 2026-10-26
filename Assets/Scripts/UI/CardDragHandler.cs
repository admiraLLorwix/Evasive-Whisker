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
        canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    public void Initialize(NetworkCard card)
    {
        CardData = card;
        IsDraggable = (card.Type == CardType.Ability);
    }

    public void SetHighlighted(bool highlighted)
    {
        Image img = GetComponentInChildren<Image>();
        if (img != null)
        {
            img.color = Color.white;
        }

        Outline outline = GetComponentInChildren<Outline>();
        if (outline == null && img != null)
        {
            outline = img.gameObject.AddComponent<Outline>();
            outline.effectDistance = new Vector2(5f, -5f);
        }

        if (outline != null)
        {
            outline.effectColor = new Color(1f, 0.85f, 0.2f, 1f);
            outline.enabled = highlighted;
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!IsDraggable) return;

        originalPosition = rectTransform.anchoredPosition;
        originalParent = transform.parent;
        originalSiblingIndex = transform.GetSiblingIndex();

        transform.SetParent(canvas.transform, true);
        canvasGroup.blocksRaycasts = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!IsDraggable) return;
        rectTransform.anchoredPosition += eventData.delta / canvas.scaleFactor;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!IsDraggable) return;

        if (canvasGroup != null) canvasGroup.blocksRaycasts = true;

        float handYThreshold = Screen.height * 0.35f;

        if (transform.position.y > handYThreshold)
        {
            Debug.Log($"<color=green>[Card Play Success] Dropped {CardData.CardId}</color>");

            PlayerHandController.Instance.RemoveCard(CardData);

            if (TurnActionUI.Instance != null)
            {
                TurnActionUI.Instance.HideAllActions();
            }

            GameManager.Instance.SubmitPlayAbilityCardServerRpc(CardData);

            Destroy(gameObject);
        }
        else
        {
            transform.SetParent(originalParent, true);
            transform.SetSiblingIndex(originalSiblingIndex);
            rectTransform.anchoredPosition = originalPosition;
            if (PlayerHandController.Instance != null)
            {
                PlayerHandController.Instance.UpdateHandFanLayout();
            }
        }
    }
}