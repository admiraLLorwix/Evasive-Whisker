using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using EvasiveWhisker.Cards;

public class PlayerHandController : MonoBehaviour
{
    public static PlayerHandController Instance { get; private set; }

    [Header("hand setup")]
    [SerializeField] private Transform handCardContainer;
    [SerializeField] private GameObject handCardPrefab;

    [Header("detective slots")]
    [SerializeField] private Transform detectiveSlotContainer;  // originally `myDetectiveSlot`; empty containing the prefab
    [SerializeField] private TMP_Text myDetectiveNameText;
    [SerializeField] private Transform abilitySlotContainer;  // originally `myActiveAbilitySlot`; empty container for ability slot

    private bool isPlayAbilityActive = false;
    private List<NetworkCard> currentCards = new List<NetworkCard>();

    private void Awake()
    {
        Instance = this;
        if (abilitySlotContainer != null)
        {
            abilitySlotContainer.gameObject.SetActive(false);
        }
    }

    public void SetLocalDetectiveCat(DetectiveCatType catType)
    {
        Sprite sprite = CardDatabase.Instance != null
            ? CardDatabase.Instance.GetLocalHandCardSprite(catType)
            : null;

        if (sprite != null && detectiveSlotContainer != null)
        {
            GameObject cardPrefab = detectiveSlotContainer.GetComponent<GameObject>();
            CardController prefabController = cardPrefab.GetComponent<CardController>();

            // TODO: dapat Card object yung mapapasa sa InitializeCard instead of enum nung detectivecattype
            //prefabController.InitializeCard(catType);
            //detectiveSlotContainer.sprite = sprite;
            detectiveSlotContainer.gameObject.SetActive(true);
        }

        if (myDetectiveNameText != null)
        {
            myDetectiveNameText.text = catType.ToString();
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

        // clean reset of hand card container
        foreach (Transform child in handCardContainer)
        {
            Destroy(child.gameObject);
        }

        // assignment of cards
        for (int i = 0; i < cards.Length; i++)
        {
            NetworkCard netCard = cards[i];
            AddCard(netCard);
            UpdateHandFanLayout();

            yield return new WaitForSeconds(0.2f);
        }
    }

    public void AddCard(NetworkCard card)
    {
        currentCards.Add(card);

        GameObject cardObj = Instantiate(handCardPrefab, handCardContainer);
		Card cardData = CardDatabase.Instance != null
				? CardDatabase.Instance.GetCard(card.CardId)
				: null;
		CardController controller = cardObj.GetComponent<CardController>();

		if (cardData != null) controller.InitializeCard(cardData);

		//Sprite cardSprite = CardDatabase.Instance != null
		//    ? CardDatabase.Instance.GetCardSprite(card.CardId)
		//    : null;

		//Image img = cardObj.GetComponentInChildren<Image>();
		//if (img != null)
		//{
		//    if (cardSprite != null)
		//    {
		//        img.sprite = cardSprite;
		//        img.color = Color.white;
		//    }
		//    else
		//    {
		//        img.color = (card.Type == CardType.WhiskerBlower) ? Color.yellow : Color.cyan;
		//    }
		//}

		//TMP_Text label = cardObj.GetComponentInChildren<TMP_Text>();
		//if (label != null)
		//{
		//    label.text = $"{card.CardId}\n({card.Type})";
		//}

		Button btn = cardObj.GetComponentInChildren<Button>();
        if (btn != null)
        {
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => OnCardClicked(card));
        }

        var dragHandler = cardObj.GetComponent<CardDragHandler>();
        if (dragHandler == null) dragHandler = cardObj.AddComponent<CardDragHandler>();
        dragHandler.Initialize(card);
    }

    public void RemoveCard(NetworkCard card)
    {
        currentCards.Remove(card);

        for (int i = 0; i < handCardContainer.childCount; i++)
        {
            var dragHandler = handCardContainer.GetChild(i).GetComponent<CardDragHandler>();
            if (dragHandler != null && dragHandler.NetworkCard.Equals(card))
            {
                Destroy(handCardContainer.GetChild(i).gameObject);
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
            var cardVisual = handCardContainer.GetChild(i).GetComponent<HandCardVisual>();
            if (cardVisual != null)
            {
                cardVisual.SetFanPosition(i, total);
            }
        }
    }

    public void SetAbilityCardsPlayable(bool playable)
    {
        isPlayAbilityActive = playable;

        for (int i = 0; i < handCardContainer.childCount; i++)
        {
            var cardObj = handCardContainer.GetChild(i);
            var dragHandler = cardObj.GetComponent<CardDragHandler>();

            bool isAbility = (i < currentCards.Count && currentCards[i].Type == CardType.Ability);

            if (isAbility && playable)
            {
                Outline outline = cardObj.GetComponentInChildren<Outline>();
                if (outline == null)
                {
                    var img = cardObj.GetComponentInChildren<Image>();
                    if (img != null) outline = img.gameObject.AddComponent<Outline>();
                }
                if (outline != null)
                {
                    outline.effectDistance = new Vector2(5, -5);
                    outline.effectColor = new Color(1f, 0.85f, 0.2f, 1f);
                    outline.enabled = true;
                }
            }
            else
            {
                Outline outline = cardObj.GetComponentInChildren<Outline>();
                if (outline != null) outline.enabled = false;
            }
        }
    }

    public int GetCustodyCardCount()
    {
        return currentCards.FindAll(c => c.Type == CardType.Custody).Count;
    }

    public bool HasAbilityCards()
    {
        return currentCards.Exists(c => c.Type == CardType.Ability);
    }

    private void OnCardClicked(NetworkCard netCard)
    {
        Debug.Log($"<color=white>{netCard.CardId} ({netCard.Type}) | PlayMode: {isPlayAbilityActive}</color>");

        if (GameManager.Instance != null && GameManager.Instance.CurrentState.Value == GameState.PlayerTurn_DiscardPrompt)
        {
            if (netCard.Type == CardType.WhiskerBlower)
            {
                Debug.LogWarning("<color=red>the WhiskerBlower cannot be discarded</color>");
                return;
            }
            RemoveCard(netCard);
            GameManager.Instance.SubmitDiscardServerRpc(netCard);
            return;
        }

        if (isPlayAbilityActive)
        {
            if (netCard.Type == CardType.Ability)
            {
                Debug.Log($"<color=cyan>Play: {netCard.CardId}</color>");

                RemoveCard(netCard);

                SetAbilityCardsPlayable(false);

                GameManager.Instance.SubmitPlayAbilityCardServerRpc(netCard);
            }
            else
            {
                Debug.LogWarning("non-Ability card. click an Ability card to play it!");
            }
        }
    }
}