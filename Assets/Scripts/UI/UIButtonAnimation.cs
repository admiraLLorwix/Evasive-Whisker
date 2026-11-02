using UnityEngine;
using System.Collections;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class UIButtonAnimation : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [SerializeField] private float hoverScale = 1.08f;
    [SerializeField] private float hoverSpeed = 16f;
    [SerializeField] private float punchScale = 0.92f;
    [SerializeField] private AudioSource clickAudioSource;

    private RectTransform rectTransform;
    private Vector3 defaultScale;
    private Vector3 targetScale;
    private Button button;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        button = GetComponent<Button>();
        defaultScale = rectTransform.localScale;
        targetScale = defaultScale;

        if (clickAudioSource == null)
        {
            var clickObj = GameObject.Find("Click");
            if (clickObj != null) clickAudioSource = clickObj.GetComponent<AudioSource>();
        }
    }

    private void OnEnable()
    {
        if (rectTransform != null)
        {
            rectTransform.localScale = defaultScale;
            targetScale = defaultScale;
        }
    }

    private void Update()
    {
        if (rectTransform == null) return;
        rectTransform.localScale = Vector3.Lerp(rectTransform.localScale, targetScale, Time.unscaledDeltaTime * hoverSpeed);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (button != null && !button.interactable) return;
        targetScale = defaultScale * hoverScale;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        targetScale = defaultScale;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (button != null && !button.interactable) return;
        targetScale = defaultScale * punchScale;

        if (clickAudioSource != null)
        {
            clickAudioSource.Play();
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        targetScale = defaultScale * hoverScale;
    }
}