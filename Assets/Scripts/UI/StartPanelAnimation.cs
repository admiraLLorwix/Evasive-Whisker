using System.Collections;
using UnityEngine;
using TMPro;

public class StartPanelAnimation : MonoBehaviour
{
    [Header("Logo Target")]
    [SerializeField] private RectTransform logoTransform;

    [Header("Tap To Start Text Target")]
    [SerializeField] private RectTransform tapToStartTextTransform;

    [Header("Logo Stamp Settings")]
    [SerializeField] private float stampDuration = 0.4f;//laggy look if >0.8
    [SerializeField] private float initialScaleMultiplier = 3.0f;

    [Header("Logo Idle Animation Settings")]
    [SerializeField] private float floatSpeed = 2f;
    [SerializeField] private float floatAmplitude = 8f;
    [SerializeField] private float tiltAngle = 1.5f;

    [Header("Tap Text Pulse Settings")]
    [SerializeField] private float pulseSpeed = 3f;//def 2f, adjust if slow
    [SerializeField] private float pulseScaleAmount = 0.08f;

    private Vector3 originalLogoScale;
    private Vector3 originalLogoPosition;
    private Vector3 originalTextScale;
    private CanvasGroup logoCanvasGroup;
    private bool isStampCompleted = false;

    private void Awake()
    {
        if (logoTransform != null)
        {
            originalLogoScale = logoTransform.localScale;
            originalLogoPosition = logoTransform.anchoredPosition;

            logoCanvasGroup = logoTransform.GetComponent<CanvasGroup>();
            if (logoCanvasGroup == null)
            {
                logoCanvasGroup = logoTransform.gameObject.AddComponent<CanvasGroup>();
            }
        }

        if (tapToStartTextTransform != null)
        {
            originalTextScale = tapToStartTextTransform.localScale;
        }
    }

    private void OnEnable()
    {
        StartAnimations();
    }

    public void StartAnimations()
    {
        StopAllCoroutines();
        if (logoTransform != null)
        {
            StartCoroutine(AnimateLogoStamp());
        }
        if (tapToStartTextTransform != null)
        {
            StartCoroutine(AnimateTapTextPulse());
        }
    }

    private IEnumerator AnimateLogoStamp()
    {
        isStampCompleted = false;
        float elapsed = 0f;

        Vector3 startScale = originalLogoScale * initialScaleMultiplier;
        logoTransform.localScale = startScale;
        logoCanvasGroup.alpha = 0f;

        while (elapsed < stampDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / stampDuration;

            float scaleProgress = 1f + 2.70158f * Mathf.Pow(t - 1f, 3f) + 1.70158f * Mathf.Pow(t - 1f, 2f);

            logoTransform.localScale = Vector3.LerpUnclamped(startScale, originalLogoScale, scaleProgress);
            logoCanvasGroup.alpha = Mathf.Lerp(0f, 1f, t * 2f);

            yield return null;
        }

        logoTransform.localScale = originalLogoScale;
        logoCanvasGroup.alpha = 1f;
        isStampCompleted = true;

        while (true)
        {
            float time = Time.time * floatSpeed;
            float newY = originalLogoPosition.y + Mathf.Sin(time) * floatAmplitude;
            float newTilt = Mathf.Cos(time * 0.5f) * tiltAngle;

            logoTransform.anchoredPosition = new Vector2(originalLogoPosition.x, newY);
            logoTransform.localRotation = Quaternion.Euler(0, 0, newTilt);

            yield return null;
        }
    }

    private IEnumerator AnimateTapTextPulse()
    {
        while (true)
        {
            float wave = (Mathf.Sin(Time.time * pulseSpeed) + 1f) / 2f;
            float scaleFactor = 1f + (wave * pulseScaleAmount);

            tapToStartTextTransform.localScale = originalTextScale * scaleFactor;

            yield return null;
        }
    }
}