using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CardVisualInteraction : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler,
    IPointerDownHandler, IPointerUpHandler
{
    [SerializeField] private RectTransform visualRoot;

    [SerializeField] private float maxTiltAngle = 8f;
    [SerializeField] private float arcDropAmount = 14f;

    [SerializeField] private float hoverScale = 1.15f;
    [SerializeField] private float hoverLiftY = 35f;

    [SerializeField] private float inspectScale = 1.6f;
    [SerializeField] private float holdDurationToInspect = 0.25f; [SerializeField] private float inspectYOffset = -100f;

    private Vector2 targetPos = Vector2.zero;
    private Quaternion targetRot = Quaternion.identity;
    private Vector3 targetScale = Vector3.one;

    private Vector2 defaultArcPos = Vector2.zero;
    private Quaternion defaultArcRot = Quaternion.identity;

    private bool isHovered = false;
    private bool isHolding = false;
    private bool isInspecting = false;
    private float holdTimer = 0f;
    private Canvas cardCanvas;
    private Canvas mainGameCanvas;

    private void Awake()
    {
        if (visualRoot == null && transform.childCount > 0)
        {
            visualRoot = transform.GetChild(0).GetComponent<RectTransform>();
        }
        mainGameCanvas = GetComponentInParent<Canvas>();
        cardCanvas = gameObject.GetComponent<Canvas>();
        if (cardCanvas == null)
        {
            cardCanvas = gameObject.AddComponent<Canvas>();
            gameObject.AddComponent<GraphicRaycaster>();
        }
        cardCanvas.overrideSorting = true;
        cardCanvas.sortingOrder = 10;

    }

    private void Update()
    {
        if (visualRoot == null) return;

        visualRoot.anchoredPosition = Vector2.Lerp(visualRoot.anchoredPosition, targetPos, Time.deltaTime * 18f);
        visualRoot.localRotation = Quaternion.Slerp(visualRoot.localRotation, targetRot, Time.deltaTime * 18f);
        visualRoot.localScale = Vector3.Lerp(visualRoot.localScale, targetScale, Time.deltaTime * 18f);

        if (isHolding && !isInspecting)
        {
            holdTimer += Time.deltaTime;
            if (holdTimer >= holdDurationToInspect)
            {
                TriggerInspect();
            }
        }
    }

    public void SetFanPosition(int cardIndex, int totalCards)
    {
        if (totalCards <= 1)
        {
            defaultArcPos = Vector2.zero;
            defaultArcRot = Quaternion.identity;
        }
        else
        {
            float t = ((float)cardIndex / (totalCards - 1) * 2f) - 1f;
            float zTilt = -t * maxTiltAngle;
            defaultArcRot = Quaternion.Euler(0, 0, zTilt);

            float yDrop = -(t * t) * arcDropAmount;
            defaultArcPos = new Vector2(0, yDrop);
        }

        if (!isHovered && !isInspecting)
        {
            targetPos = defaultArcPos;
            targetRot = defaultArcRot;
            targetScale = Vector3.one;
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (isInspecting) return;
        isHovered = true;

        targetPos = defaultArcPos + new Vector2(0, hoverLiftY);
        targetRot = Quaternion.identity;
        targetScale = Vector3.one * hoverScale;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (isInspecting) return;
        isHovered = false;
        isHolding = false;
        holdTimer = 0f;

        targetPos = defaultArcPos;
        targetRot = defaultArcRot;
        targetScale = Vector3.one;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        isHolding = true;
        holdTimer = 0f;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isHolding = false;
        holdTimer = 0f;

        if (isInspecting)
        {
            isInspecting = false;
            cardCanvas.sortingOrder = 10;
            targetPos = isHovered ? (defaultArcPos + new Vector2(0, hoverLiftY)) : defaultArcPos;
            targetRot = isHovered ? Quaternion.identity : defaultArcRot;
            targetScale = isHovered ? (Vector3.one * hoverScale) : Vector3.one;
        }
    }

    private void TriggerInspect()
    {
        isInspecting = true;
        cardCanvas.sortingOrder = 100;
        Vector2 screenCenter = new Vector2(Screen.width / 2f, Screen.height / 2f);
        Vector2 localPoint;
        RectTransform parentRect = transform.parent as RectTransform;
        RectTransform cardRect = transform as RectTransform;

        if (parentRect != null && mainGameCanvas != null && cardRect != null)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRect,
                screenCenter,
                mainGameCanvas.worldCamera,
                out localPoint
            );
            localPoint.y += inspectYOffset;
            targetPos = localPoint - cardRect.anchoredPosition;
        }
        else
        {
            targetPos = new Vector2(0, 250f);
        }

        targetRot = Quaternion.identity;
        targetScale = Vector3.one * inspectScale;
    }
}