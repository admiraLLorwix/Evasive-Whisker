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

    //scatter
    private bool isScatterPickActive = false;
    private bool scatterPickSubmitted = false;
    private bool confirmIsScatter = false;
    private string scatterNeighborName = "";
    private NetworkCard pendingScatterCard;
    private bool passIsTrapGive = false;
    private string passLabel = "";
    public bool IsScatterPickActive => isScatterPickActive && !scatterPickSubmitted;
    public int HandCount => currentCards.Count;
    private Coroutine reorderRoutine;
    public bool IsActiveAbilitySlotOccupied => myActiveAbilitySlot != null && myActiveAbilitySlot.gameObject.activeSelf;
    private void OnDisable()
    {
        reorderRoutine = null;
    }
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

    public void RemoveCardObject(GameObject cardObj, NetworkCard card)
    {
        currentCards.Remove(card);

        if (cardObj != null)
        {
            cardObj.transform.SetParent(null);
            Destroy(cardObj);
        }

        UpdateHandFanLayout();
    }

    public void UpdateHandFanLayout()
    {
        if (reorderRoutine != null)
        {
            StopCoroutine(reorderRoutine);
            reorderRoutine = null;
        }

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
        confirmIsScatter = false;
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
        if (confirmIsScatter)
        {
            confirmIsScatter = false;
            scatterPickSubmitted = true;
            SetAllCardsHighlighted(false);
            if (passIsTrapGive)
            {
                TurnActionUI.Instance?.SetInstructionText("Passing your card...", passLabel);
                GameManager.Instance?.SubmitTrapGivePickServerRpc(pendingScatterCard);
            }
            else
            {
                TurnActionUI.Instance?.SetInstructionText("Waiting for the other players to pick...", passLabel);
                GameManager.Instance?.SubmitScatterPickServerRpc(pendingScatterCard);
            }
            return;
        }
        RemoveCard(pendingDiscardCard);
        GameManager.Instance.SubmitDiscardServerRpc(pendingDiscardCard);
    }

    private void OnCancelDiscardClicked()
    {
        confirmIsScatter = false;
        if (discardConfirmModal != null) discardConfirmModal.SetActive(false);
    }

    public void BeginScatterPick(string neighborName) =>
        BeginPassPick("<color=orange>SCATTER!</color>", neighborName, false);

    public void BeginTrapGivePick(string trapName, string recipientName) =>
        BeginPassPick($"<color=red>{trapName.ToUpper()}!</color>", recipientName, true);

    private void BeginPassPick(string label, string recipientName, bool trapGive)

    {
        isScatterPickActive = true;
        scatterPickSubmitted = false;
        confirmIsScatter = false;
        passIsTrapGive = trapGive;
        passLabel = label;
        scatterNeighborName = recipientName;

        SetAbilityCardsPlayable(false);
        TurnActionUI.Instance?.HideAllActions();
        SetAllCardsHighlighted(true);
        TurnActionUI.Instance?.SetInstructionText($"Drag a card up to pass it to <color=yellow>{recipientName}</color>", label);
    }

    public void PromptScatterConfirmation(NetworkCard card)
    {
        pendingScatterCard = card;
        confirmIsScatter = true;

        if (discardConfirmModal != null)
        {
            discardConfirmModal.SetActive(true);
            if (confirmPromptText != null)
            {
                confirmPromptText.text = $"Pass <color=yellow>{card.CardId}</color> to <color=yellow>{scatterNeighborName}</color>?";
            }
        }
    }
    public void ReopenScatterPick()
    {
        scatterPickSubmitted = false;
        if (isScatterPickActive) BeginPassPick(passLabel, scatterNeighborName, passIsTrapGive);
    }

    public void EndScatterPick()
    {
        isScatterPickActive = false;
        scatterPickSubmitted = false;
        if (confirmIsScatter && discardConfirmModal != null) discardConfirmModal.SetActive(false);
        confirmIsScatter = false;
        SetAllCardsHighlighted(false);
    }
    
    //hand shuffle from game 7: repo#13
    public void ApplyShuffledOrder(NetworkCard[] serverOrder)
    {
        if (handCardContainer == null || serverOrder == null)
        {
            Debug.LogWarning("[shuffle][hand] applyshuffleorder: missing container");
            return;
        }

        var remaining = new List<Transform>();
        for (int i = 0; i < handCardContainer.childCount; i++) remaining.Add(handCardContainer.GetChild(i));
        if (remaining.Count < 2)
        {
            Debug.LogWarning($"[shuffle][hand] applyshuffleorder ignored: only {remaining.Count} card visuals in the hand container");
            return;
        }
        int visualCount = remaining.Count;

        var startPos = new Dictionary<Transform, Vector2>();
        foreach (Transform tr in remaining)
        {
            RectTransform rt = tr as RectTransform;
            if (rt != null) startPos[tr] = rt.anchoredPosition;
        }

        var ordered = new List<Transform>();
        foreach (NetworkCard card in serverOrder)
        {
            int idx = remaining.FindIndex(tr =>
            {
                var h = tr.GetComponent<CardDragHandler>();
                return h != null && h.CardData.Equals(card);
            });
            if (idx < 0) continue;

            ordered.Add(remaining[idx]);
            remaining.RemoveAt(idx);
        }
        Debug.Log($"[shuffle][hand] server order={serverOrder.Length}, visuals={visualCount}, matched={ordered.Count}, unmatched visuals={remaining.Count}");

        ordered.AddRange(remaining);

        for (int i = 0; i < ordered.Count; i++) ordered[i].SetSiblingIndex(i);

        var newModel = new List<NetworkCard>();
        var leftovers = new List<NetworkCard>(currentCards);
        foreach (Transform tr in ordered)
        {
            var h = tr.GetComponent<CardDragHandler>();
            if (h == null) continue;
            newModel.Add(h.CardData);
            leftovers.Remove(h.CardData);
        }
        newModel.AddRange(leftovers);
        currentCards = newModel;

        UpdateHandFanLayout();

        var moves = new List<(RectTransform rt, Vector2 start, Vector2 end)>();
        foreach (Transform tr in ordered)
        {
            RectTransform rt = tr as RectTransform;
            if (rt == null || !startPos.TryGetValue(tr, out Vector2 start)) continue;

            Vector2 end = rt.anchoredPosition;
            rt.anchoredPosition = start;
            moves.Add((rt, start, end));
        }

        if (moves.Count > 0 && isActiveAndEnabled) reorderRoutine = StartCoroutine(ReorderAnimationRoutine(moves));
    }
    private IEnumerator ReorderAnimationRoutine(List<(RectTransform rt, Vector2 start, Vector2 end)> moves)
    {
        const float gatherTime = 0.25f;
        const float spreadTime = 0.35f;

        float t = 0f;
        while (t < gatherTime)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / gatherTime);
            foreach (var m in moves)
                if (m.rt != null) m.rt.anchoredPosition = Vector2.Lerp(m.start, new Vector2(0f, m.start.y), k);
            yield return null;
        }

        t = 0f;
        while (t < spreadTime)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / spreadTime);
            foreach (var m in moves)
                if (m.rt != null) m.rt.anchoredPosition = Vector2.Lerp(new Vector2(0f, m.start.y), m.end, k);
            yield return null;
        }

        foreach (var m in moves)
            if (m.rt != null) m.rt.anchoredPosition = m.end;

        reorderRoutine = null;
    }

    public void ApplyScatterResult(bool gave, NetworkCard passed, bool got, NetworkCard received)
    {
        if (gave) RemoveCard(passed);
        if (got) AddCard(received);
    }

    private void SetAllCardsHighlighted(bool on)
    {
        if (handCardContainer == null) return;
        for (int i = 0; i < handCardContainer.childCount; i++)
        {
            var dragHandler = handCardContainer.GetChild(i).GetComponent<CardDragHandler>();
            if (dragHandler != null) dragHandler.SetHighlighted(on);
        }
    }



    public int GetCustodyCardCount() => currentCards.FindAll(c => c.Type == CardType.Custody).Count;
    public bool HasAbilityCards() => currentCards.Exists(c => c.Type == CardType.Ability);
}