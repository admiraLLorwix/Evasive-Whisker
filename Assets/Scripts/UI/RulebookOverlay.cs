
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class RulebookController : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Image pageImageDisplay;
    [SerializeField] private TMP_Text pageNumText;
    [SerializeField] private Button nextBtn;
    [SerializeField] private Button prevBtn;
    [SerializeField] private Button closeBtn;

    [Header("Book Content")]
    [SerializeField] private Sprite[] rulePages;

    [Header("Page Flip Animation")]
    [SerializeField] private float flipDuration = 0.55f;
    [SerializeField] private float rotationAngle = 12f;
    [SerializeField] private float compressionAmount = 0.12f;
    [SerializeField] private float overshootAmount = 0.025f;

    private int _currentPageIndex = 0;
    private bool _isAnimating = false;

    private RectTransform _pageRect;
    private Vector3 _originalScale;
    private Quaternion _originalRotation;

    private void Awake()
    {
        if (pageImageDisplay != null)
        {
            _pageRect = pageImageDisplay.GetComponent<RectTransform>();
            _originalScale = _pageRect.localScale;
            _originalRotation = _pageRect.localRotation;
        }

        if (nextBtn != null)
            nextBtn.onClick.AddListener(NextPage);

        if (prevBtn != null)
            prevBtn.onClick.AddListener(PrevPage);

        if (closeBtn != null)
            closeBtn.onClick.AddListener(CloseBook);
    }

    public void OpenBook()
    {
        gameObject.SetActive(true);

        _currentPageIndex = 0;
        _isAnimating = false;

        ResetPageAnimation();
        UpdatePageDisplay();
    }

    public void CloseBook()
    {
        StopAllCoroutines();

        _isAnimating = false;
        ResetPageAnimation();

        gameObject.SetActive(false);
    }

    private void NextPage()
    {
        if (_isAnimating) return;
        if (rulePages == null || rulePages.Length == 0) return;

        if (_currentPageIndex < rulePages.Length - 1)
            StartCoroutine(AnimatePageFlip(1));
    }

    private void PrevPage()
    {
        if (_isAnimating) return;

        if (_currentPageIndex > 0)
            StartCoroutine(AnimatePageFlip(-1));
    }

    private IEnumerator AnimatePageFlip(int direction)
    {
        _isAnimating = true;

        UpdateButtonStates();

        float elapsed = 0f;
        float halfDuration = flipDuration / 2f;

        Vector3 scale = _originalScale;

        // PHASE 1: Paper begins folding
        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / halfDuration);

            // Smooth acceleration
            float eased = Mathf.SmoothStep(0f, 1f, t);

            // Slight horizontal compression
            scale.x = _originalScale.x *
                      (1f - compressionAmount * eased);

            // Subtle vertical paper movement
            scale.y = _originalScale.y *
                      (1f + 0.015f * Mathf.Sin(t * Mathf.PI));

            _pageRect.localScale = scale;

            // Rotate in the direction of the flip
            float angle = Mathf.Lerp(
                0f,
                rotationAngle * direction,
                eased
            );

            _pageRect.localRotation =
                _originalRotation *
                Quaternion.Euler(0f, 0f, angle);

            yield return null;
        }

        // SWITCH PAGE AT THE MIDDLE
        _currentPageIndex += direction;
        UpdatePageDisplay();

        // PHASE 2: New page settles into position
        elapsed = 0f;

        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / halfDuration);

            // Smooth deceleration
            float eased = Mathf.SmoothStep(0f, 1f, t);

            // Restore paper dimensions with a slight overshoot
            float settle = Mathf.Sin(t * Mathf.PI);

            scale.x = _originalScale.x *
                      (1f - compressionAmount * (1f - eased));

            scale.y = _originalScale.y *
                      (1f + overshootAmount * settle);

            _pageRect.localScale = scale;

            // Gradually restore rotation
            float angle = Mathf.Lerp(
                rotationAngle * direction,
                0f,
                eased
            );

            _pageRect.localRotation =
                _originalRotation *
                Quaternion.Euler(0f, 0f, angle);

            yield return null;
        }

        ResetPageAnimation();

        _isAnimating = false;

        UpdatePageDisplay();
    }

    private void ResetPageAnimation()
    {
        if (_pageRect == null) return;

        _pageRect.localScale = _originalScale;
        _pageRect.localRotation = _originalRotation;
    }

    private void UpdateButtonStates()
    {
        if (prevBtn != null)
            prevBtn.interactable = false;

        if (nextBtn != null)
            nextBtn.interactable = false;
    }

    private void UpdatePageDisplay()
    {
        if (rulePages == null ||
            rulePages.Length == 0 ||
            pageImageDisplay == null)
            return;

        pageImageDisplay.sprite = rulePages[_currentPageIndex];

        if (pageNumText != null)
        {
            pageNumText.text =
                $"Page {_currentPageIndex + 1} of {rulePages.Length}";
        }

        if (!_isAnimating)
        {
            if (prevBtn != null)
                prevBtn.interactable = _currentPageIndex > 0;

            if (nextBtn != null)
                nextBtn.interactable =
                    _currentPageIndex < rulePages.Length - 1;
        }
    }
}
