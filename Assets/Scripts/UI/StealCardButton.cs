using UnityEngine;
using UnityEngine.EventSystems;

public class StealCardButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private RectTransform rectTransform;
    private Vector2 defaultPos;
    private Quaternion defaultRot;

    private Vector2 targetPos;
    private Quaternion targetRot;
    private Vector3 targetScale = Vector3.one;

    private int originalSiblingIndex;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
    }

    private void Update()
    {
        if (rectTransform == null) return;

        rectTransform.anchoredPosition = Vector2.Lerp(rectTransform.anchoredPosition, targetPos, Time.deltaTime * 18f);
        rectTransform.localRotation = Quaternion.Slerp(rectTransform.localRotation, targetRot, Time.deltaTime * 18f);
        rectTransform.localScale = Vector3.Lerp(rectTransform.localScale, targetScale, Time.deltaTime * 18f);
    }

    public void SetArcPosition(Vector2 pos, Quaternion rot)
    {
        defaultPos = pos;
        defaultRot = rot;
        targetPos = defaultPos;
        targetRot = defaultRot;
        targetScale = Vector3.one;

        if (rectTransform != null)
        {
            rectTransform.anchoredPosition = pos;
            rectTransform.localRotation = rot;
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        originalSiblingIndex = transform.GetSiblingIndex();
        transform.SetAsLastSibling();

        targetPos = defaultPos + new Vector2(0f, 35f);
        targetRot = Quaternion.identity;
        targetScale = Vector3.one * 1.15f;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        transform.SetSiblingIndex(originalSiblingIndex);
        targetPos = defaultPos;
        targetRot = defaultRot;
        targetScale = Vector3.one;
    }
}