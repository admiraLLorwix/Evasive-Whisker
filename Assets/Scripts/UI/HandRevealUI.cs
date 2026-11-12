using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using EvasiveWhisker.Cards;
using System.Collections;

public class HandRevealUI : MonoBehaviour
{
    public static HandRevealUI Instance { get; private set; }

    [SerializeField] private GameObject panel;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private RectTransform cardContainer;
    [SerializeField] private Button closeButton;
    [SerializeField] private Vector2 cardSize = new Vector2(70f, 98f);
    [SerializeField] private float cardGap = 18f;

    private readonly List<GameObject> spawnedCards = new List<GameObject>();
    private Coroutine autoHideRoutine;
    private System.Action<int> pickCallback;
    private System.Action cancelCallback;

    private void Awake()
    {
        Instance = this;
        if (panel != null) panel.SetActive(false);
        if (closeButton != null) closeButton.onClick.AddListener(OnCloseClicked);
    }

    public void ShowPicker(string title, NetworkCard[] cards, System.Action<int> onPick, System.Action onCancel)
    {
        if (panel == null || cardContainer == null) return;

        if (autoHideRoutine != null) { StopCoroutine(autoHideRoutine); autoHideRoutine = null; }
        ClearCards();
        pickCallback = onPick;
        cancelCallback = onCancel;
        if (titleText != null) titleText.text = title;

        int count = cards != null ? cards.Length : 0;
        float width = cardContainer.rect.width > 10f ? cardContainer.rect.width : 900f;
        float height = cardContainer.rect.height > 10f ? cardContainer.rect.height : 420f;

        float scale = 1f;
        int perRow = 1;
        int rows = 1;
        for (; scale >= 0.3f; scale -= 0.1f)
        {
            float w = cardSize.x * scale;
            float h = cardSize.y * scale;
            perRow = Mathf.Max(1, Mathf.FloorToInt((width + cardGap) / (w + cardGap)));
            rows = Mathf.Max(1, Mathf.CeilToInt(count / (float)perRow));
            if (rows * (h + cardGap) - cardGap <= height) break;
        }

        float cw = cardSize.x * scale;
        float ch = cardSize.y * scale;
        for (int i = 0; i < count; i++)
        {
            int row = i / perRow;
            int col = i % perRow;
            int inRow = Mathf.Min(perRow, count - row * perRow);

            float x = (col - (inRow - 1) * 0.5f) * (cw + cardGap);
            float y = ((rows - 1) * 0.5f - row) * (ch + cardGap);

            GameObject obj = CreateCard(cards[i], new Vector2(x, y), 0f);
            var rt = (RectTransform)obj.transform;
            rt.sizeDelta = new Vector2(cw, ch);

            var image = obj.GetComponent<Image>();
            image.raycastTarget = true;
            var btn = obj.AddComponent<Button>();
            btn.targetGraphic = image;
            int index = i;
            btn.onClick.AddListener(() => OnPickClicked(index));

            spawnedCards.Add(obj);
        }

        panel.SetActive(true);
        panel.transform.SetAsLastSibling();
    }

    private void OnPickClicked(int index)
    {
        var callback = pickCallback;
        Hide();
        callback?.Invoke(index);
    }

    private void OnCloseClicked()
    {
        var cancel = cancelCallback;
        Hide();
        cancel?.Invoke();
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
        pickCallback = null;
        cancelCallback = null;

        if (autoHideRoutine != null) { StopCoroutine(autoHideRoutine); autoHideRoutine = null; }
        ClearCards();
        if (titleText != null) titleText.text = title;

        int count = cards != null ? cards.Length : 0;
        if (count == 0) return;

        float maxTiltAngle = 12f;
        float arcDropAmount = 20f;
        float cardSpacing = 100f;
        if (count > 1) cardSpacing = Mathf.Min(100f, (cardContainer.rect.width - 80f) / (count - 1));

        for (int i = 0; i < count; i++)
        {
            float t = (count <= 1) ? 0f : ((float)i / (count - 1) * 2f) - 1f;
            float x = t * ((count - 1) * cardSpacing * 0.5f);
            float y = -(t * t) * arcDropAmount;
            float zRot = -t * maxTiltAngle;

            GameObject obj = CreateCard(cards[i], new Vector2(x, y), zRot);
            obj.transform.SetAsLastSibling();

            var fanVisual = obj.GetComponent<StealCardButton>();
            if (fanVisual == null) fanVisual = obj.AddComponent<StealCardButton>();
            fanVisual.SetArcPosition(new Vector2(x, y), Quaternion.Euler(0, 0, zRot));

            spawnedCards.Add(obj);
        }

        panel.SetActive(true);
        panel.transform.SetAsLastSibling();
    }

    public void Hide()
    {
        pickCallback = null;
        cancelCallback = null;
        if (autoHideRoutine != null) { StopCoroutine(autoHideRoutine); autoHideRoutine = null; }
        ClearCards();
        if (panel != null) panel.SetActive(false);
    }

    private GameObject CreateCard(NetworkCard netCard, Vector2 pos, float rot)
    {
        var obj = new GameObject($"RevealCard_{netCard.CardId}", typeof(RectTransform), typeof(Image));
        obj.transform.SetParent(cardContainer, false);

        var rt = (RectTransform)obj.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = cardSize;
        rt.anchoredPosition = pos;
        rt.localRotation = Quaternion.Euler(0, 0, rot);

        var image = obj.GetComponent<Image>();
        image.raycastTarget = true;

        Sprite sprite = CardDatabase.Instance != null ? CardDatabase.Instance.GetCardSprite(netCard.CardId) : null;
        if (sprite == null)
        {
            Card so = netCard.GetScriptableObject();
            if (so != null && so.frontfaceFrame != null)
            {
                sprite = Sprite.Create(so.frontfaceFrame, new Rect(0, 0, so.frontfaceFrame.width, so.frontfaceFrame.height), new Vector2(0.5f, 0.5f));
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