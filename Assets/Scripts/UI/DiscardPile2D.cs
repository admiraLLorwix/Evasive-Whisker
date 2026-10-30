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

    private List<GameObject> pileCards = new List<GameObject>();

    private void Awake()
    {
        Instance = this;
    }

    public void AddCardToDiscard(NetworkCard card)
    {
        if (discardContainer == null || cardUIPrefab == null) return;

        GameObject cardObj = Instantiate(cardUIPrefab, discardContainer);
        RectTransform rt = cardObj.GetComponent<RectTransform>();

        float randomAngle = Random.Range(-maxRotationTilt, maxRotationTilt);
        float jitterX = Random.Range(-maxPositionJitter, maxPositionJitter);
        float jitterY = Random.Range(-maxPositionJitter, maxPositionJitter);

        rt.anchoredPosition = new Vector2(jitterX, jitterY);
        rt.localRotation = Quaternion.Euler(0f, 0f, randomAngle);
        rt.localScale = new Vector3(0.55f, 0.55f, 1f);

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

        if (pileCards.Count > 6)
        {
            Destroy(pileCards[0]);
            pileCards.RemoveAt(0);
        }
    }
}