using UnityEngine;
using UnityEngine.EventSystems;

public class HandCardVisual : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler,
    IPointerDownHandler, IPointerUpHandler
{
    [Header("visual child")]
    [SerializeField] private RectTransform cardVisual;

    [Header("fan settings")]
    [SerializeField] private float maxTiltAngle = 9f;
    [SerializeField] private float arcDropAmount = 14f;

    [Header("hover settings")]
    [SerializeField] private float hoverScale = 1.15f;
    [SerializeField] private float hoverLiftY = 35f;

    [Header("hold inspect settings")]
    [SerializeField] private float inspectScale = 1.6f;
    [SerializeField] private float holdDuration = 0.25f;




    private Vector2 targetPos = Vector2.zero;
    private Quaternion targetRot = Quaternion.identity;
    private Vector3 targetScale = Vector3.one;

    private Vector2 defaultFanPos = Vector2.zero;
    private Quaternion defaultFanRot = Quaternion.identity;

    private bool isHovered = false;
    private bool isHolding = false;
    private bool isInspecting = false;
    private float holdTimer = 0f;

    private Canvas mainCanvas;

    private void Awake()
    {
        if (cardVisual == null && transform.childCount > 0)
        {
            cardVisual = transform.GetChild(0).GetComponent<RectTransform>();
        }
        mainCanvas = GetComponentInParent<Canvas>();
    }

    private void Update()
    {
        if (cardVisual == null) return;

        cardVisual.anchoredPosition = Vector2.Lerp(cardVisual.anchoredPosition, targetPos, Time.deltaTime * 16f);
        cardVisual.localRotation = Quaternion.Slerp(cardVisual.localRotation, targetRot, Time.deltaTime * 16f);
        cardVisual.localScale = Vector3.Lerp(cardVisual.localScale, targetScale, Time.deltaTime * 16f);

        if (isHolding && !isInspecting)
        {
            holdTimer += Time.deltaTime;
            if (holdTimer >= holdDuration)
            {
                TriggerInspect();
            }
        }
    }

    public void SetFanPosition(int index, int totalCards)
    {
        if (totalCards <= 1)
        {
            defaultFanPos = Vector2.zero;
            defaultFanRot = Quaternion.identity;
        }
        else
        {
            float t = ((float)index / (totalCards - 1) * 2f) - 1f;

            float zTilt = -t * maxTiltAngle;
            defaultFanRot = Quaternion.Euler(0, 0, zTilt);

            float yDrop = -(t * t) * arcDropAmount;
            defaultFanPos = new Vector2(0, yDrop);
        }

        if (!isHovered && !isInspecting)
        {
            targetPos = defaultFanPos;
            targetRot = defaultFanRot;
            targetScale = Vector3.one;
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (isInspecting) return;
        isHovered = true;

        targetPos = defaultFanPos + new Vector2(0, hoverLiftY);
        targetRot = Quaternion.identity;
        targetScale = Vector3.one * hoverScale;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (isInspecting) return;
        isHovered = false;
        isHolding = false;
        holdTimer = 0f;

        targetPos = defaultFanPos;
        targetRot = defaultFanRot;
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
            targetPos = isHovered ? (defaultFanPos + new Vector2(0, hoverLiftY)) : defaultFanPos;
            targetRot = isHovered ? Quaternion.identity : defaultFanRot;
            targetScale = isHovered ? (Vector3.one * hoverScale) : Vector3.one;
        }
    }

    private void TriggerInspect()
    {
        isInspecting = true;

        if (mainCanvas != null)
        {
            Vector3 screenCenterWorld = mainCanvas.transform.position;
            Vector3 localCenter = transform.InverseTransformPoint(screenCenterWorld);
            targetPos = new Vector2(localCenter.x, localCenter.y);
        }
        else
        {
            targetPos = new Vector2(0, 250f);
        }

        targetRot = Quaternion.identity;
        targetScale = Vector3.one * inspectScale;
    }
}