using System.Collections;
using UnityEngine;

public class UIModalJuice : MonoBehaviour
{
    [SerializeField] private float openDuration = 0.25f;
    [SerializeField]
    private AnimationCurve popCurve = new AnimationCurve(
        new Keyframe(0f, 0.75f),
        new Keyframe(0.65f, 1.05f),
        new Keyframe(1f, 1.0f)
    );

    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;
    private Coroutine openRoutine;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();

        if (canvasGroup != null)
        {
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }
    }

    private void OnEnable()
    {
        if (openRoutine != null) StopCoroutine(openRoutine);
        openRoutine = StartCoroutine(AnimateOpen());
    }

    private IEnumerator AnimateOpen()
    {
        float elapsed = 0f;
        rectTransform.localScale = Vector3.one * 0.75f;
        if (canvasGroup != null) canvasGroup.alpha = 0f;

        while (elapsed < openDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / openDuration);

            float scale = popCurve.Evaluate(t);
            rectTransform.localScale = Vector3.one * scale;

            if (canvasGroup != null) canvasGroup.alpha = Mathf.Lerp(0f, 1f, t);

            yield return null;
        }

        rectTransform.localScale = Vector3.one;
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }
        openRoutine = null;
    }
}