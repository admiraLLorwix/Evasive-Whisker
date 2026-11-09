using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;
using EvasiveWhisker.Cards;

public class DiscardPile2D : MonoBehaviour
{
    public static DiscardPile2D Instance { get; private set; }

    [SerializeField] private RectTransform discardContainer;
    [SerializeField] private GameObject cardUIPrefab;

    [SerializeField] private float maxRotationTilt = 16f;
    [SerializeField] private float maxPositionJitter = 10f;
    [SerializeField] private float carduiwidth = 200f;
    [SerializeField] private float carduiheight = 250f;

    private List<GameObject> pileCards = new List<GameObject>();
    private List<NetworkCard> pileData = new List<NetworkCard>();
    private const int MaxVisibleCards = 6;

    private void Awake()
    {
        Instance = this;
    }

    public void AddCardToDiscard(NetworkCard card)
    {
        pileData.Add(card);
        SpawnPileVisual(card);
    }

    public void RemoveCardFromDiscard(NetworkCard card)
    {
        for (int i = pileData.Count - 1; i >= 0; i--)
        {
            if (pileData[i].Equals(card))
            {
                pileData.RemoveAt(i);
                break;
            }
        }

        foreach (GameObject obj in pileCards) if (obj != null) Destroy(obj);
        pileCards.Clear();

        int start = Mathf.Max(0, pileData.Count - MaxVisibleCards);
        for (int i = start; i < pileData.Count; i++) SpawnPileVisual(pileData[i]);
    }

    private void SpawnPileVisual(NetworkCard card)
    {
        if (discardContainer == null || cardUIPrefab == null) return;

        GameObject cardObj = Instantiate(cardUIPrefab, discardContainer);

        RectTransform rt = cardObj.GetComponent<RectTransform>();
        Transform visualRoot = cardObj.transform.Find("VisualRoot");
        RectTransform visualRt = visualRoot != null ? visualRoot.GetComponent<RectTransform>() : null;

        if (visualRt != null)
        {
            visualRt.sizeDelta = new Vector2(carduiwidth, carduiheight);
        }
        if (rt != null)
        {
            rt.sizeDelta = new Vector2(carduiwidth, carduiheight);
        }

        float randomAngle = Random.Range(-maxRotationTilt, maxRotationTilt);
        float jitterX = Random.Range(-maxPositionJitter, maxPositionJitter);
        float jitterY = Random.Range(-maxPositionJitter, maxPositionJitter);

        rt.anchoredPosition = new Vector2(jitterX, jitterY);
        rt.localRotation = Quaternion.Euler(0f, 0f, randomAngle);
        rt.localScale = new Vector3(1f, 1f, 1f);

        Sprite cardSprite = CardDatabase.Instance != null
            ? CardDatabase.Instance.GetCardSprite(card.CardId)
            : null;

        Card cardSO = card.GetScriptableObject();
        if (cardSprite == null && cardSO != null && cardSO.frontfaceFrame != null)
        {
            cardSprite = Sprite.Create(
                cardSO.frontfaceFrame,
                new Rect(0, 0, cardSO.frontfaceFrame.width, cardSO.frontfaceFrame.height),
                new Vector2(0.5f, 0.5f)
            );
        }

        Image img = cardObj.GetComponentInChildren<Image>();
        if (img != null)
        {
            if (cardSprite != null)
            {
                img.sprite = cardSprite;
                img.color = Color.white;
            }
            else
            {
                img.color = Color.cyan;
            }
        }

        var drag = cardObj.GetComponent<CardDragHandler>();
        if (drag != null) Destroy(drag);

        var visual = cardObj.GetComponent<CardVisualInteraction>();
        if (visual != null) Destroy(visual);

        Button btn = cardObj.GetComponentInChildren<Button>();
        if (btn != null) btn.interactable = false;

        pileCards.Add(cardObj);

        if (pileCards.Count > MaxVisibleCards)
        {
            Destroy(pileCards[0]);
            pileCards.RemoveAt(0);
        }
    }

    [ContextMenu("test discard")]
    public void TestSpawnCard()
    {
        NetworkCard dummyCard = new NetworkCard("Swipe", CardType.Trap);

        AddCardToDiscard(dummyCard);
    }
}