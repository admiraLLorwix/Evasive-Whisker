using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using EvasiveWhisker.Cards;

public class PlayerHandController : MonoBehaviour
{
    public static PlayerHandController Instance { get; private set; }

    [SerializeField] private Transform handCardContainer;
    [SerializeField] private GameObject handCardPrefab;

    [SerializeField] private Image myDetectiveSlot;
    [SerializeField] private Image myActiveAbilitySlot;

    [Header("confirm modl")]
    [SerializeField] private GameObject discardConfirmModal;
    [SerializeField] private TMP_Text confirmPromptText;
    [SerializeField] private Button confirmDiscardBtn;
    [SerializeField] private Button cancelDiscardBtn;

    [SerializeField] private float cardSpacingX = 140f;

    private List<NetworkCard> currentCards = new List<NetworkCard>();
    private NetworkCard pendingDiscardCard;

    private void Awake()
    {
        Instance = this;

        if (myActiveAbilitySlot != null) myActiveAbilitySlot.gameObject.SetActive(false);

        if (confirmDiscardBtn != null) confirmDiscardBtn.onClick.AddListener(OnConfirmDiscardClicked);
        if (cancelDiscardBtn != null) cancelDiscardBtn.onClick.AddListener(OnCancelDiscardClicked);
        if (discardConfirmModal != null) discardConfirmModal.SetActive(false);
    }

    public void SetLocalDetectiveCat(DetectiveCatType catType)
    {
        Sprite sprite = CardDatabase.Instance != null
            ? CardDatabase.Instance.GetLocalHandCardSprite(catType)
            : null;

        if (sprite != null && myDetectiveSlot != null)
        {
            myDetectiveSlot.sprite = sprite;
            myDetectiveSlot.color = Color.white;
            myDetectiveSlot.gameObject.SetActive(true);
        }
    }

    public void SetActiveAbility(string cardId)
    {
        if (myActiveAbilitySlot == null) return;

        Sprite sprite = CardDatabase.Instance != null
            ? CardDatabase.Instance.GetCardSprite(cardId)
            : null;

        if (sprite != null)
        {
            myActiveAbilitySlot.sprite = sprite;
            myActiveAbilitySlot.color = Color.white;
            myActiveAbilitySlot.gameObject.SetActive(true);
            Debug.Log($"<color=green>[Active Ability] displayed {cardId}</color>");
        }
        else
        {
            Debug.LogWarning($"[Active Ability] no sprite: {cardId}");
        }
    }

    public void ClearActiveAbility()
    {
        if (myActiveAbilitySlot != null)
        {
            myActiveAbilitySlot.sprite = null;
            myActiveAbilitySlot.gameObject.SetActive(false);
        }
    }

    public void SetHand(NetworkCard[] cards)
    {
        currentCards = new List<NetworkCard>(cards);
        StartCoroutine(DealCardsRoutine(cards));
    }

    private IEnumerator DealCardsRoutine(NetworkCard[] cards)
    {
        if (handCardContainer == null || handCardPrefab == null) yield break;

        foreach (Transform child in handCardContainer)
        {
            Destroy(child.gameObject);
        }

        for (int i = 0; i < cards.Length; i++)
        {
            SpawnCardObject(cards[i]);
            yield return new WaitForSeconds(0.2f);
        }
    }

    public void AddCard(NetworkCard card)
    {
        currentCards.Add(card);
        SpawnCardObject(card);
    }

    private void SpawnCardObject(NetworkCard netCard)
    {
        GameObject cardObj = Instantiate(handCardPrefab, handCardContainer);


        Sprite cardSprite = CardDatabase.Instance != null
            ? CardDatabase.Instance.GetCardSprite(netCard.CardId)
            : null;

        Card cardSO = netCard.GetScriptableObject();
        if (cardSprite == null && cardSO != null && cardSO.frontfaceFrame != null)
        {
            cardSprite = Sprite.Create(
                cardSO.frontfaceFrame,
                new Rect(0, 0, cardSO.frontfaceFrame.width, cardSO.frontfaceFrame.height),
                new Vector2(0.5f, 0.5f)
            );
        }

        Transform visualRoot = cardObj.transform.Find("VisualRoot"); //direct na sa visroot
        Image cardImage = visualRoot != null ? visualRoot.GetComponent<Image>() : cardObj.GetComponentInChildren<Image>();

        if (cardImage != null)
        {
            if (cardSprite != null)
            {
                cardImage.sprite = cardSprite;
                cardImage.color = Color.white;
            }
            else
            {
                cardImage.color = (netCard.Type == CardType.WhiskerBlower) ? Color.yellow : Color.cyan;
                Debug.LogWarning($"[PlayerHand] missing sprite: '{netCard.CardId}'!");
            }
        }

        var dragHandler = cardObj.GetComponent<CardDragHandler>();
        if (dragHandler == null) dragHandler = cardObj.AddComponent<CardDragHandler>();
        dragHandler.Initialize(netCard);

        UpdateHandFanLayout();
    }

    //todo: (bug) not adjusting when stolen
    public void RemoveCard(NetworkCard card)
    {
        currentCards.Remove(card);

        for (int i = 0; i < handCardContainer.childCount; i++)
        {
            var dragHandler = handCardContainer.GetChild(i).GetComponent<CardDragHandler>();
            if (dragHandler != null && dragHandler.CardData.Equals(card))
            {
                Transform cardTransform = handCardContainer.GetChild(i);
                cardTransform.SetParent(null);
                Destroy(cardTransform.gameObject);
                break;
            }
        }
        UpdateHandFanLayout();
    }

    public void UpdateHandFanLayout()
    {
        int total = handCardContainer.childCount;
        for (int i = 0; i < total; i++)
        {
            var cardTransform = handCardContainer.GetChild(i) as RectTransform;
            var cardVisual = handCardContainer.GetChild(i).GetComponent<CardVisualInteraction>();

            if (cardTransform != null)
            {
                float t = (total <= 1) ? 0f : ((float)i / (total - 1) * 2f) - 1f;
                float xOffset = t * (cardSpacingX * (total - 1) * 0.5f);

                cardTransform.anchoredPosition = new Vector2(xOffset, 0f);
            }

            if (cardVisual != null)
            {
                cardVisual.SetFanPosition(i, total);
            }
        }
    }

    public void SetAbilityCardsPlayable(bool playable)
    {
        for (int i = 0; i < handCardContainer.childCount; i++)
        {
            var dragHandler = handCardContainer.GetChild(i).GetComponent<CardDragHandler>();
            if (dragHandler != null)
            {
                if (dragHandler.CardData.Type == CardType.Ability)
                {
                    dragHandler.IsDraggable = playable;
                    dragHandler.SetHighlighted(playable);
                }
                else
                {
                    dragHandler.IsDraggable = false;
                    dragHandler.SetHighlighted(false);
                }
            }
        }
    }

    public void PromptDiscardConfirmation(NetworkCard card)
    {
        pendingDiscardCard = card;
        if (discardConfirmModal != null)
        {
            discardConfirmModal.SetActive(true);
            if (confirmPromptText != null)
            {
                confirmPromptText.text = $"Are you sure you want to discard <color=yellow>{card.CardId}</color>?";
            }
        }
    }

    private void OnConfirmDiscardClicked()
    {
        if (discardConfirmModal != null) discardConfirmModal.SetActive(false);
        RemoveCard(pendingDiscardCard);
        GameManager.Instance.SubmitDiscardServerRpc(pendingDiscardCard);
    }

    private void OnCancelDiscardClicked()
    {
        if (discardConfirmModal != null) discardConfirmModal.SetActive(false);
    }

    public int GetCustodyCardCount() => currentCards.FindAll(c => c.Type == CardType.Custody).Count;
    public bool HasAbilityCards() => currentCards.Exists(c => c.Type == CardType.Ability);
}