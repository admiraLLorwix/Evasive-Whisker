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
    //testing for card flight
    [SerializeField] private Transform drawDeckFlightSource;
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

    //counterplay window
    private HashSet<string> reactionValidIds = null;
    public bool IsReactionActive => reactionValidIds != null;
    public bool IsValidReactionCard(NetworkCard card) => reactionValidIds != null && reactionValidIds.Contains(card.CardId);

    private Coroutine reorderRoutine;
    public bool IsActiveAbilitySlotOccupied => myActiveAbilitySlot != null && myActiveAbilitySlot.gameObject.activeSelf;
    private Dictionary<GameObject, Coroutine> activePulses = new Dictionary<GameObject, Coroutine>();

    private void OnDisable()
    {
        reorderRoutine = null;
        StopAllPulses();
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

        DetectiveCatUI catUi = DetectiveCatUI.Instance != null ? DetectiveCatUI.Instance 
            : FindAnyObjectByType<DetectiveCatUI>(FindObjectsInactive.Include);
        if (catUi != null) catUi.SetCat(catType);
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

        Sprite cardSprite = CardDatabase.Instance != null
            ? CardDatabase.Instance.GetCardSprite(card.CardId)
            : null;

        Vector3 startPos = drawDeckFlightSource != null ? drawDeckFlightSource.position : transform.position + new Vector3(0, 300, 0);
        Vector3 targetPos = handCardContainer.position;

        if (CardFlightManager.Instance != null)
        {
            CardFlightManager.Instance.FlyCard(startPos, targetPos, cardSprite, () =>
            {
                SpawnCardObject(card);
            });
        }
        else
        {
            SpawnCardObject(card);
        }
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
                StopCardPulse(cardTransform.gameObject);
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
            StopCardPulse(cardObj);
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
                    if (playable) StartCardPulse(handCardContainer.GetChild(i).gameObject);
                    else StopCardPulse(handCardContainer.GetChild(i).gameObject);
                }
                else
                {
                    dragHandler.IsDraggable = false;
                    dragHandler.SetHighlighted(false);
                    StopCardPulse(handCardContainer.GetChild(i).gameObject); 
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
    public void HideDiscardConfirm()
    {
        if (!confirmIsScatter && discardConfirmModal != null) discardConfirmModal.SetActive(false);
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

    //for reaction windwo
    public void BeginReaction(string description, NetworkCard[] legalCards, float seconds, string catName = null)
    {
        reactionValidIds = new HashSet<string>();
        var names = new List<string>();
        foreach (NetworkCard card in legalCards)
        {
            if (reactionValidIds.Add(card.CardId)) names.Add(card.CardId);
        }
        if (!string.IsNullOrEmpty(catName)) names.Add(catName);

        if (handCardContainer != null)
        {
            for (int i = 0; i < handCardContainer.childCount; i++)
            {
                var dragHandler = handCardContainer.GetChild(i).GetComponent<CardDragHandler>();
                if (dragHandler != null) dragHandler.SetHighlighted(IsValidReactionCard(dragHandler.CardData));
            }
        }

        TurnActionUI.Instance?.ShowTemporaryNotification(
            $"<color=red>{description}!</color> Drag <color=yellow>{string.Join(" / ", names)}</color> up to respond", seconds);
    }

    public void EndReaction()
    {
        DetectiveCatUI.Instance?.EndReaction();
        if (reactionValidIds == null) return;
        reactionValidIds = null;
        SetAllCardsHighlighted(false);
    }
    public void CommitReaction(NetworkCard card)
    {
        EndReaction();
        GameManager.Instance?.SubmitReactionServerRpc(card);
    }
    public int GetCustodyCardCount() => currentCards.FindAll(c => c.Type == CardType.Custody).Count;
    public bool HasAbilityCards() => currentCards.Exists(c => c.Type == CardType.Ability);
    public bool HasPlayableAbilityCards() => currentCards.Exists(c =>
        c.Type == CardType.Ability && !(CardEffectRegistry.OccupiesSlot(c.CardId) && IsActiveAbilitySlotOccupied));


    private void StartCardPulse(GameObject cardObj)
    {
        if (cardObj == null || activePulses.ContainsKey(cardObj)) return;
        activePulses[cardObj] = StartCoroutine(PulseRoutine(cardObj));
    }

    private void StopCardPulse(GameObject cardObj)
    {
        if (cardObj == null) return;
        if (activePulses.ContainsKey(cardObj))
        {
            if (activePulses[cardObj] != null) StopCoroutine(activePulses[cardObj]);
            activePulses.Remove(cardObj);
            
            if (cardObj != null) cardObj.transform.localScale = Vector3.one; 
        }
    }

    private void StopAllPulses()
    {
        foreach (var pulse in activePulses.Values)
        {
            if (pulse != null) StopCoroutine(pulse);
        }
        activePulses.Clear();
    }

    private IEnumerator PulseRoutine(GameObject cardObj)
    {
        while (true)
        {
            var dragHandler = cardObj.GetComponent<CardDragHandler>();
            bool isBusy = dragHandler != null && dragHandler.IsDragging;

            if (!isBusy)
            {
                float pulse = 1.0f + (Mathf.Sin(Time.time * 3f) * 0.015f);
                cardObj.transform.localScale = new Vector3(pulse, pulse, 1f);
            }

            yield return null;
        }
    }
}