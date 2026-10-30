using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class SlotInspectInteraction : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler,
    IPointerDownHandler, IPointerUpHandler
{
    [SerializeField] private float hoverScale = 1.08f;
    [SerializeField] private float inspectScale = 1.6f;
    [SerializeField] private float holdDuration = 0.25f;
    private RectTransform rectTransform;
    private Image slotImage;
    private Vector2 defaultPos; private Vector2 targetPos; private Vector3 targetScale = Vector3.one;
    private Quaternion defaultRot; private Quaternion targetRot;
    private bool isHovered = false; private bool isHolding = false;
    private bool isInspecting = false; private float holdTimer = 0f;
    private int originalSiblingIndex; private Canvas mainCanvas;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        slotImage = GetComponent<Image>();
        mainCanvas = GetComponentInParent<Canvas>();

        if (rectTransform != null)
        {
            defaultPos = rectTransform.anchoredPosition;
            defaultRot = rectTransform.localRotation;
            targetPos = defaultPos;
            targetRot = defaultRot;
        }
    }

    private void Update()
    {
        if (rectTransform == null) return;
        if (slotImage != null && (slotImage.sprite == null || slotImage.color.a < 0.1f)) return;

        rectTransform.anchoredPosition = Vector2.Lerp(rectTransform.anchoredPosition, targetPos, Time.deltaTime * 18f);
        rectTransform.localRotation = Quaternion.Slerp(rectTransform.localRotation, targetRot, Time.deltaTime * 18f);
        rectTransform.localScale = Vector3.Lerp(rectTransform.localScale, targetScale, Time.deltaTime * 18f);

        if (isHolding && !isInspecting)
        {
            holdTimer += Time.deltaTime;
            if (holdTimer >= holdDuration)
            {
                TriggerInspect();
            }
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (slotImage == null || slotImage.sprite == null || slotImage.color.a < 0.1f) return;
        if (isInspecting) return;

        isHovered = true;
        originalSiblingIndex = transform.GetSiblingIndex();
        transform.SetAsLastSibling();

        targetScale = Vector3.one * hoverScale;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (isInspecting) return;
        isHovered = false;
        isHolding = false;
        holdTimer = 0f;

        transform.SetSiblingIndex(originalSiblingIndex);
        targetPos = defaultPos;
        targetRot = defaultRot;
        targetScale = Vector3.one;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (slotImage == null || slotImage.sprite == null || slotImage.color.a < 0.1f) return;
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
            transform.SetSiblingIndex(originalSiblingIndex);
            targetPos = defaultPos;
            targetRot = defaultRot;
            targetScale = isHovered ? (Vector3.one * hoverScale) : Vector3.one;
        }
    }

    private void TriggerInspect()
    {
        isInspecting = true;
        transform.SetAsLastSibling();

        if (mainCanvas != null)
        {
            Vector3 screenCenterWorld = mainCanvas.transform.position;
            Vector3 localCenter = transform.parent.InverseTransformPoint(screenCenterWorld);
            targetPos = new Vector2(localCenter.x, localCenter.y);
        }
        else
        {
            targetPos = Vector2.zero;
        }

        targetRot = Quaternion.identity;
        targetScale = Vector3.one * inspectScale;
    }
}