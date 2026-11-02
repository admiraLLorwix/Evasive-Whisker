using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;

public class CardGalleryController : MonoBehaviour
{
    [System.Serializable]
    public class CardEntry
    {
        [Header("Card Button")]
        public Button cardButton;

        [Header("Card Preview")]
        public Sprite previewImage;

        [Header("Card Information")]
        public string cardName;

        [TextArea(2, 5)]
        public string description;
    }

    [Header("Card List")]
    [SerializeField]
    private List<CardEntry> cards =
        new List<CardEntry>();

    [Header("Preview UI")]
    [SerializeField] private Image previewImageDisplay;
    [SerializeField] private TMP_Text previewNameText;
    [SerializeField] private TMP_Text previewDescriptionText;

    [Header("Default Selection")]
    [SerializeField] private int defaultCardIndex = 0;
    [SerializeField] private bool showDefaultPreview = true;

    [Header("Card Selection Animation")]
    [SerializeField] private float selectionScale = 1.12f;
    [SerializeField] private float selectionSpeed = 10f;

    [Header("Selection Outline")]
    [SerializeField]
    private Color outlineColor =
        new Color(1f, 0.8f, 0.2f, 1f);

    [SerializeField]
    private Vector2 outlineDistance =
        new Vector2(3f, 3f);

    [Header("Preview Animation")]
    [SerializeField] private float previewFadeDuration = 0.2f;

    [Header("Card Click Sound")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip cardClickSound;
    [SerializeField, Range(0f, 1f)] private float clickVolume = 0.7f;

    private int _selectedIndex = -1;

    private Vector3[] _originalScales;
    private Outline[] _outlines;
    private UnityAction[] _buttonActions;

    private CanvasGroup _previewCanvasGroup;

    private Coroutine _selectionCoroutine;
    private Coroutine _previewCoroutine;

    private void Awake()
    {
        InitializeCards();
        InitializePreview();

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();

            if (audioSource == null)
                audioSource = gameObject.AddComponent<AudioSource>();
        }

        audioSource.playOnAwake = false;
    }

    private void Start()
    {
        if (showDefaultPreview && cards.Count > 0)
        {
            SelectCard(
                Mathf.Clamp(defaultCardIndex, 0, cards.Count - 1)
            );
        }
    }

    private void PlayCardClickSound()
    {
        if (audioSource != null && cardClickSound != null)
        {
            audioSource.PlayOneShot(cardClickSound, clickVolume);
        }
    }

    // INITIALIZE CARD BUTTONS
    private void InitializeCards()
    {
        _originalScales = new Vector3[cards.Count];
        _outlines = new Outline[cards.Count];
        _buttonActions = new UnityAction[cards.Count];

        for (int i = 0; i < cards.Count; i++)
        {
            int index = i;

            if (cards[i].cardButton == null)
                continue;

            Transform cardTransform =
                cards[i].cardButton.transform;

            _originalScales[i] = cardTransform.localScale;

            // Automatically create selection outline
            Graphic targetGraphic =
                cards[i].cardButton.targetGraphic;

            if (targetGraphic != null)
            {
                Outline outline =
                    targetGraphic.GetComponent<Outline>();

                if (outline == null)
                    outline = targetGraphic.gameObject.AddComponent<Outline>();

                outline.effectColor = outlineColor;
                outline.effectDistance = outlineDistance;
                outline.useGraphicAlpha = true;
                outline.enabled = false;

                _outlines[i] = outline;
            }

            // Automatically connect button
            _buttonActions[i] = () => SelectCard(index);

            cards[i].cardButton.onClick.AddListener(
                _buttonActions[i]
            );
        }
    }

    // INITIALIZE PREVIEW IMAGE
    private void InitializePreview()
    {
        if (previewImageDisplay == null)
            return;

        previewImageDisplay.preserveAspect = true;

        _previewCanvasGroup =
            previewImageDisplay.GetComponent<CanvasGroup>();

        if (_previewCanvasGroup == null)
        {
            _previewCanvasGroup =
                previewImageDisplay.gameObject.AddComponent<CanvasGroup>();
        }

        _previewCanvasGroup.alpha = 1f;
        _previewCanvasGroup.interactable = false;
        _previewCanvasGroup.blocksRaycasts = false;
    }

    // SELECT CARD
    public void SelectCard(int index)
    {
        if (index < 0 || index >= cards.Count)
            return;

        PlayCardClickSound();

        if (_selectedIndex == index)
            return;

        int previousIndex = _selectedIndex;

        _selectedIndex = index;

        // Update selection outline
        UpdateOutlines(index);

        // Animate selected card
        if (_selectionCoroutine != null)
            StopCoroutine(_selectionCoroutine);

        _selectionCoroutine =
            StartCoroutine(AnimateCardSelection());

        // Update card information
        UpdateCardInformation(index);

        // Animate preview image
        if (_previewCoroutine != null)
            StopCoroutine(_previewCoroutine);

        _previewCoroutine =
            StartCoroutine(AnimatePreview(index));
    }

    // CARD SELECTION ANIMATION
    private IEnumerator AnimateCardSelection()
    {
        Vector3[] startingScales =
            new Vector3[cards.Count];

        for (int i = 0; i < cards.Count; i++)
        {
            if (cards[i].cardButton != null)
            {
                startingScales[i] =
                    cards[i].cardButton.transform.localScale;
            }
        }

        float progress = 0f;

        while (progress < 1f)
        {
            progress += Time.deltaTime * selectionSpeed;

            float eased = Mathf.SmoothStep(
                0f, 1f, Mathf.Clamp01(progress)
            );

            for (int i = 0; i < cards.Count; i++)
            {
                if (cards[i].cardButton == null)
                    continue;

                Vector3 targetScale = _originalScales[i];

                if (i == _selectedIndex)
                {
                    targetScale *= selectionScale;
                }

                cards[i].cardButton.transform.localScale =
                    Vector3.Lerp(
                        startingScales[i],
                        targetScale,
                        eased
                    );
            }

            yield return null;
        }

        // Ensure exact final scale
        for (int i = 0; i < cards.Count; i++)
        {
            if (cards[i].cardButton == null)
                continue;

            cards[i].cardButton.transform.localScale =
                _originalScales[i] *
                (i == _selectedIndex ? selectionScale : 1f);
        }

        _selectionCoroutine = null;
    }

    // UPDATE SELECTION OUTLINES
    private void UpdateOutlines(int selectedIndex)
    {
        for (int i = 0; i < _outlines.Length; i++)
        {
            if (_outlines[i] != null)
            {
                _outlines[i].enabled = i == selectedIndex;
            }
        }
    }

    // PREVIEW IMAGE FADE ANIMATION
    private IEnumerator AnimatePreview(int index)
    {
        CardEntry selectedCard = cards[index];

        if (previewImageDisplay != null)
        {
            // Fade out
            yield return FadePreview(1f, 0f);

            // Change image
            previewImageDisplay.sprite =
                selectedCard.previewImage;

            previewImageDisplay.enabled =
                selectedCard.previewImage != null;

            // Fade in
            if (selectedCard.previewImage != null)
            {
                yield return FadePreview(0f, 1f);
            }
        }

        _previewCoroutine = null;
    }

    // FADE HELPER
    private IEnumerator FadePreview(float start, float end)
    {
        if (_previewCanvasGroup == null)
            yield break;

        float elapsed = 0f;

        while (elapsed < previewFadeDuration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(
                elapsed / previewFadeDuration
            );

            float eased = Mathf.SmoothStep(0f, 1f, t);

            _previewCanvasGroup.alpha =
                Mathf.Lerp(start, end, eased);

            yield return null;
        }

        _previewCanvasGroup.alpha = end;
    }

    // UPDATE CARD TEXT
    private void UpdateCardInformation(int index)
    {
        CardEntry selectedCard = cards[index];

        if (previewNameText != null)
        {
            previewNameText.text = selectedCard.cardName;
        }

        if (previewDescriptionText != null)
        {
            previewDescriptionText.text =
                selectedCard.description;
        }
    }

    private void OnDestroy()
    {
        // Remove only listeners created by this script
        for (int i = 0; i < cards.Count; i++)
        {
            if (cards[i].cardButton != null &&
                _buttonActions != null &&
                i < _buttonActions.Length &&
                _buttonActions[i] != null)
            {
                cards[i].cardButton.onClick.RemoveListener(
                    _buttonActions[i]
                );
            }
        }
    }
}
