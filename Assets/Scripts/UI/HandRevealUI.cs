using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using EvasiveWhisker.Cards;
using System.Collections;
//used for robin paw and correct guess on custodian
public class HandRevealUI : MonoBehaviour
{
    public static HandRevealUI Instance { get; private set; }

    [SerializeField] private GameObject panel;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private RectTransform cardContainer;
    [SerializeField] private Button closeButton;
    [SerializeField] private Vector2 cardSize = new Vector2(130f, 190f);
    [SerializeField] private float cardGap = 18f;

    private readonly List<GameObject> spawnedCards = new List<GameObject>();
    private Coroutine autoHideRoutine;

    private void Awake()
    {
        Instance = this;

        if (panel != null) panel.SetActive(false);
        if (closeButton != null) closeButton.onClick.AddListener(Hide);
    }
    public void ShowTimed(string title, NetworkCard[] cards, float seconds)
    {
        Show(title, cards);
        autoHideRoutine = StartCoroutine(AutoHideRoutine(seconds));
    }

    private IEnumerator AutoHideRoutine(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        autoHideRoutine = null;
        Hide();
    }
    public void Show(string title, NetworkCard[] cards)
    {
        if (panel == null || cardContainer == null) return;

        if (autoHideRoutine != null)
        {
            StopCoroutine(autoHideRoutine);
            autoHideRoutine = null;
        }
        ClearCards();
        if (titleText != null) titleText.text = title;

        int count = cards != null ? cards.Length : 0;

        float availableWidth = cardContainer.rect.width;
        float step = cardSize.x + cardGap;
        if (count > 1 && availableWidth > cardSize.x)
        {
            step = Mathf.Min(step, (availableWidth - cardSize.x) / (count - 1));
        }

        for (int i = 0; i < count; i++)
        {
            float x = (i - (count - 1) * 0.5f) * step;
            spawnedCards.Add(CreateCard(cards[i], x));
        }

        panel.SetActive(true);
        panel.transform.SetAsLastSibling();
    }

    public void Hide()
    {
        if (autoHideRoutine != null)
        {
            StopCoroutine(autoHideRoutine);
            autoHideRoutine = null;
        }
        ClearCards();
        if (panel != null) panel.SetActive(false);
    }

    private GameObject CreateCard(NetworkCard netCard, float x)
    {
        var obj = new GameObject($"RevealCard_{netCard.CardId}", typeof(RectTransform), typeof(Image));
        obj.transform.SetParent(cardContainer, false);

        var rt = (RectTransform)obj.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = cardSize;
        rt.anchoredPosition = new Vector2(x, 0f);

        var image = obj.GetComponent<Image>();
        image.raycastTarget = false;

        Sprite sprite = CardDatabase.Instance != null ? CardDatabase.Instance.GetCardSprite(netCard.CardId) : null;

        if (sprite == null)
        {
            Card so = netCard.GetScriptableObject();
            if (so != null && so.frontfaceFrame != null)
            {
                sprite = Sprite.Create(so.frontfaceFrame,
                    new Rect(0, 0, so.frontfaceFrame.width, so.frontfaceFrame.height),
                    new Vector2(0.5f, 0.5f));
            }
        }

        if (sprite != null)
        {
            image.sprite = sprite;
            image.color = Color.white;
        }
        else
        {
            image.color = (netCard.Type == CardType.WhiskerBlower) ? Color.yellow : Color.cyan;
            Debug.LogWarning($"[HandReveal] missing sprite: '{netCard.CardId}'");
        }

        return obj;
    }

    private void ClearCards()
    {
        foreach (var card in spawnedCards)
        {
            if (card != null) Destroy(card);
        }
        spawnedCards.Clear();
    }
}