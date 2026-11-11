using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using EvasiveWhisker.Cards;
using System.Collections;

public class ActiveAbilitySlotUI : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    public static ActiveAbilitySlotUI Instance { get; private set; }

    [SerializeField] private GameObject visualRoot;
    [SerializeField] private Image cardImage;
    [SerializeField] private TMP_Text turnTimerText;
    [SerializeField] private GameObject placeholderBorder;
    [SerializeField] private float inspectScale = 1.4f;
    [SerializeField] private float inspectYOffset = -50f;

    private bool hasAbility = false;
    public bool HasAbility => hasAbility;

    private RectTransform rectTransform;
    private Canvas mainCanvas;

    private Vector3 originalWorldPos;
    private Quaternion originalRot;
    private int originalSiblingIndex;

    private Coroutine inspectRoutine; private Canvas cardCanvas;

    private void Awake()
    {
        Instance = this;
        rectTransform = GetComponent<RectTransform>();
        mainCanvas = GetComponentInParent<Canvas>();
        cardCanvas = gameObject.GetComponent<Canvas>();
        if (cardCanvas == null)
        {
            cardCanvas = gameObject.AddComponent<Canvas>();
            gameObject.AddComponent<GraphicRaycaster>();
        }
        cardCanvas.overrideSorting = false;
        originalWorldPos = rectTransform.position;
        originalRot = rectTransform.localRotation;
        if (GetComponent<CanvasGroup>() == null)
        {
            gameObject.AddComponent<CanvasGroup>().blocksRaycasts = true;
        }
        ClearAbility();
    }

    public void SetAbility(string cardId)
    {
        hasAbility = true;
        if (visualRoot != null) visualRoot.SetActive(true);
        if (placeholderBorder != null) placeholderBorder.SetActive(false);

        if (cardImage != null && CardDatabase.Instance != null)
        {
            Sprite sprite = CardDatabase.Instance.GetCardSprite(cardId);
            if (sprite != null)
            {
                cardImage.sprite = sprite;
                cardImage.color = Color.white;
            }
        }
    }

    public void ClearAbility()
    {
        hasAbility = false;
        if (visualRoot != null) visualRoot.SetActive(false);
        if (placeholderBorder != null) placeholderBorder.SetActive(true);
        if (turnTimerText != null) turnTimerText.text = "";
    }

    public void UpdateTurnsLeft(int turns)
    {
        if (turnTimerText != null && hasAbility)
        {
            turnTimerText.text = turns.ToString();
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!hasAbility) return;

        originalSiblingIndex = transform.GetSiblingIndex();
        transform.SetAsLastSibling();
        if (cardCanvas != null)
        {
            cardCanvas.overrideSorting = true;
            cardCanvas.sortingOrder = 50;
        }
        if (inspectRoutine != null) StopCoroutine(inspectRoutine);
        inspectRoutine = StartCoroutine(AnimateInspect(true));
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!hasAbility) return;
        if (cardCanvas != null) cardCanvas.overrideSorting = false;
        if (inspectRoutine != null) StopCoroutine(inspectRoutine);
        inspectRoutine = StartCoroutine(AnimateInspect(false));
    }

    private IEnumerator AnimateInspect(bool goingToCenter)
    {
        float duration = 0.2f;
        float elapsed = 0f;

        Vector3 startWorldPos = rectTransform.position;
        Vector3 startScale = rectTransform.localScale;
        Quaternion startRot = rectTransform.localRotation;

        Vector3 targetWorldPos = originalWorldPos;
        Vector3 targetScale = Vector3.one;
        Quaternion targetRot = originalRot;

        if (goingToCenter)
        {
            Vector2 screenCenter = new Vector2(Screen.width / 2f, Screen.height / 2f);
            Vector3 worldCenter = mainCanvas.worldCamera != null
                ? mainCanvas.worldCamera.ScreenToWorldPoint(new Vector3(screenCenter.x, screenCenter.y, 10f))
                : new Vector3(screenCenter.x, screenCenter.y, 0);

            worldCenter.y += inspectYOffset;

            targetWorldPos = worldCenter;
            targetScale = Vector3.one * inspectScale;
            targetRot = Quaternion.identity;
        }

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);

            rectTransform.position = Vector3.Lerp(startWorldPos, targetWorldPos, t);
            rectTransform.localScale = Vector3.Lerp(startScale, targetScale, t);
            rectTransform.localRotation = Quaternion.Slerp(startRot, targetRot, t);

            yield return null;
        }

        rectTransform.position = targetWorldPos;
        rectTransform.localScale = targetScale;
        rectTransform.localRotation = targetRot;

        if (!goingToCenter)
        {
            transform.SetSiblingIndex(originalSiblingIndex);
            rectTransform.position = originalWorldPos;
        }

        inspectRoutine = null;
    }
}