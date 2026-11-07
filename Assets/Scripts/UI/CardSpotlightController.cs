using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using EvasiveWhisker.Cards;
using UnityEngine.EventSystems;

public class CardSpotlightController : MonoBehaviour, IPointerClickHandler
{
    public static CardSpotlightController Instance { get; private set; }

    [SerializeField] private GameObject spotlightRoot; [SerializeField] private GameObject cardRevealGroup;
    [SerializeField] private Image spotlightCardImage; [SerializeField] private Sprite defaultCardBackSprite;
    [SerializeField] private TMP_Text actionTitleText;
    [SerializeField] private Outline cardGlowOutline;
    //optional but better if set
    //necessary lang para di matakpan player hand
    [SerializeField] private Transform[] keepInFront;

    [SerializeField] private GameObject versusGroup;
    [SerializeField] private RectTransform attackerSide; [SerializeField] private Image attackerAvatar; [SerializeField] private TMP_Text versusAttackerText;
    [SerializeField] private RectTransform victimSide; [SerializeField] private Image victimAvatar; [SerializeField] private TMP_Text versusVictimText;
    [SerializeField] private RectTransform actionTextSide; [SerializeField] private TMP_Text versusActionText;
    [SerializeField] private CanvasGroup vignetteDimGroup;
    [SerializeField] private RectTransform canvasRoot;
    [SerializeField] private float shakeDuration = 0.15f;
    [SerializeField] private float shakeMagnitude = 8f;

    private Coroutine activeSpotlightRoutine;
    private bool _skipRequested = false;
    private Vector2 _canvasOriginalPos;
    private Vector2 _attackerOriginalPos;
    private Vector2 _victimOriginalPos;
    private Vector3 _actionOriginalScale;
    private void Awake()
    {
        Instance = this;
        if (canvasRoot != null) _canvasOriginalPos = canvasRoot.anchoredPosition;
        if (attackerSide != null) _attackerOriginalPos = attackerSide.anchoredPosition;
        if (victimSide != null) _victimOriginalPos = victimSide.anchoredPosition;
        if (actionTextSide != null) _actionOriginalScale = actionTextSide.localScale;
        CanvasGroup group = GetComponent<CanvasGroup>();
        if (group == null) group = gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = true;
        group.interactable = true;

        if (spotlightRoot != null) spotlightRoot.SetActive(false);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.clickCount == 2) _skipRequested = true;
    }

    public void PlayCatSpotlight(DetectiveCatType cat, string bannerMessage, Color glowColor, float duration = 2.5f)
    {
        Sprite sprite = CardDatabase.Instance != null ? CardDatabase.Instance.GetLocalHandCardSprite(cat) : null;
        PlaySpotlight("", bannerMessage, glowColor, duration, false, sprite);
    }

    public void PlaySpotlight(string cardId, string bannerMessage, Color glowColor, float duration = 2.4f, bool showBackface = false, Sprite overrideSprite = null)
    {
        if (activeSpotlightRoutine != null) StopCoroutine(activeSpotlightRoutine);
        _skipRequested = false;
        gameObject.SetActive(true);
        if (spotlightRoot != null) spotlightRoot.SetActive(true);

        if (cardRevealGroup != null) cardRevealGroup.SetActive(true);
        if (versusGroup != null) versusGroup.SetActive(false);

        transform.SetAsLastSibling();
        if (keepInFront != null)
        {
            foreach (Transform front in keepInFront)
                if (front != null && front.parent == transform.parent) front.SetAsLastSibling();
        }

        activeSpotlightRoutine = StartCoroutine(CardRevealRoutine(cardId, bannerMessage, glowColor, duration, showBackface, overrideSprite));
    }

    private IEnumerator CardRevealRoutine(string cardId, string bannerMessage, Color glowColor, float duration, bool showBackface, Sprite overrideSprite)
    {
        if (spotlightRoot == null) yield break;

        if (vignetteDimGroup != null)
        {
            vignetteDimGroup.gameObject.SetActive(true);
            vignetteDimGroup.alpha = 0f;
        }

        if (spotlightCardImage != null) spotlightCardImage.gameObject.SetActive(true);

        Sprite cardSprite = null;
        if (showBackface) cardSprite = defaultCardBackSprite;
        else
        {
            if (CardDatabase.Instance != null) cardSprite = CardDatabase.Instance.GetCardSprite(cardId);
            if (cardSprite == null && CardDatabase.Instance != null)
            {
                Card cardSO = CardDatabase.Instance.GetCard(cardId);
                if (cardSO != null && cardSO.frontfaceFrame != null)
                    cardSprite = Sprite.Create(cardSO.frontfaceFrame, new Rect(0, 0, cardSO.frontfaceFrame.width, cardSO.frontfaceFrame.height), new Vector2(0.5f, 0.5f));
            }
        }

        if (spotlightCardImage != null)
        {
            if (overrideSprite != null) cardSprite = overrideSprite;
            spotlightCardImage.sprite = cardSprite != null ? cardSprite : defaultCardBackSprite;
            spotlightCardImage.color = new Color(1, 1, 1, 0);
        }

        if (actionTitleText != null) actionTitleText.text = bannerMessage;
        if (cardGlowOutline != null) cardGlowOutline.effectColor = glowColor;

        RectTransform cardRT = spotlightCardImage.rectTransform;
        Vector2 finalPos = cardRT.anchoredPosition;
        Vector2 startPos = finalPos + new Vector2(0, 500f);
        cardRT.anchoredPosition = startPos;
        cardRT.localScale = Vector3.one * 1.3f;
        float startRotZ = Random.Range(-10f, 10f);
        float endRotZ = Random.Range(-2f, 2f);

        if (vignetteDimGroup != null)
        {
            float fadeElapsed = 0f;
            while (fadeElapsed < 0.15f)
            {
                fadeElapsed += Time.deltaTime;
                vignetteDimGroup.alpha = Mathf.Lerp(0f, 0.85f, fadeElapsed / 0.15f);
                yield return null;
            }
            vignetteDimGroup.alpha = 0.85f;
        }
        if (spotlightCardImage != null) spotlightCardImage.color = Color.white;

        float dropTimer = 0f;
        float dropDuration = 0.2f;

        while (dropTimer < dropDuration && !_skipRequested)
        {
            dropTimer += Time.deltaTime;
            float t = dropTimer / dropDuration;

            float curveT = t*t;
            cardRT.anchoredPosition = Vector2.Lerp(startPos, finalPos, curveT);
            cardRT.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(startRotZ, endRotZ, curveT));
            cardRT.localScale = Vector3.Lerp(Vector3.one * 1.3f, Vector3.one, curveT);

            yield return null;
        }

        cardRT.anchoredPosition = finalPos;
        cardRT.localRotation = Quaternion.Euler(0, 0, endRotZ);
        cardRT.localScale = Vector3.one;

        if (canvasRoot != null) StartCoroutine(ScreenShakeRoutine());

        float elapsed = dropDuration;
        while (elapsed < duration && !_skipRequested)
        {
            elapsed += Time.deltaTime;
            float pulse = 1.0f + (Mathf.Sin(elapsed * 4.5f) * 0.015f);
            cardRT.localScale = Vector3.one * pulse;
            yield return null;
        }

        HideSpotlight();
    }
    private IEnumerator ScreenShakeRoutine()
    {
        float elapsed = 0f;
        while (elapsed < shakeDuration)
        {
            elapsed += Time.deltaTime;
            float x = Random.Range(-1f, 1f) * shakeMagnitude;
            float y = Random.Range(-1f, 1f) * shakeMagnitude;
            canvasRoot.anchoredPosition = new Vector2(_canvasOriginalPos.x + x, _canvasOriginalPos.y + y);
            yield return null;
        }
        canvasRoot.anchoredPosition = _canvasOriginalPos;
    }

    public void PlayVersusSpotlight(ulong attackerId, ulong victimId, string actionText, float duration = 2.5f)
    {
        if (activeSpotlightRoutine != null) StopCoroutine(activeSpotlightRoutine);
        _skipRequested = false;
        gameObject.SetActive(true);
        if (spotlightRoot != null) spotlightRoot.SetActive(true);

        if (cardRevealGroup != null) cardRevealGroup.SetActive(false);
        if (versusGroup != null) versusGroup.SetActive(true);

        transform.SetAsLastSibling();
        if (keepInFront != null)
        {
            foreach (Transform front in keepInFront)

                if (front != null && front.parent == transform.parent) front.SetAsLastSibling();
            
        }
        string attackerName = GameManager.Instance != null ? GameManager.Instance.GetPlayerName(attackerId) : "Player 1";
        string victimName = GameManager.Instance != null ? GameManager.Instance.GetPlayerName(victimId) : "Player 2";

        if (versusAttackerText != null) versusAttackerText.text = attackerName;
        if (versusVictimText != null) versusVictimText.text = victimName;
        if (versusActionText != null) versusActionText.text = actionText;

        if (GameManager.Instance != null && CardDatabase.Instance != null)
        {
            DetectiveCatType atkCat = GameManager.Instance.GetPlayerDetective(attackerId);
            DetectiveCatType vicCat = GameManager.Instance.GetPlayerDetective(victimId);

            Sprite atkSprite = CardDatabase.Instance.GetIslandProfileSprite(atkCat);
            Sprite vicSprite = CardDatabase.Instance.GetIslandProfileSprite(vicCat);

            if (attackerAvatar != null)
            {
                attackerAvatar.sprite = atkSprite;
                attackerAvatar.gameObject.SetActive(atkSprite != null);
            }
            if (victimAvatar != null)
            {
                victimAvatar.sprite = vicSprite;
                victimAvatar.gameObject.SetActive(vicSprite != null);
            }
        }

        //if (activeSpotlightRoutine != null) StopCoroutine(activeSpotlightRoutine);
        activeSpotlightRoutine = StartCoroutine(VersusRoutine(duration));
    }

    private IEnumerator VersusRoutine(float duration)
    {
        if (vignetteDimGroup != null)
        {
            vignetteDimGroup.gameObject.SetActive(true);
            vignetteDimGroup.alpha = 0.85f;
        }

        //Transform ribbon = transform.Find("BannerRibbon");
        //if (ribbon != null) ribbon.gameObject.SetActive(true);

        Vector2 atkStartPos = _attackerOriginalPos + new Vector2(-800f, 0f);
        Vector2 vicStartPos = _victimOriginalPos + new Vector2(800f, 0f);

        if (attackerSide != null) attackerSide.anchoredPosition = atkStartPos;
        if (victimSide != null) victimSide.anchoredPosition = vicStartPos;
        if (actionTextSide != null) actionTextSide.localScale = Vector3.zero;

        float animTime = 0.3f;
        float elapsed = 0f;

        while (elapsed < animTime)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / animTime);

            if (attackerSide != null) attackerSide.anchoredPosition = Vector2.Lerp(atkStartPos, _attackerOriginalPos, t);
            if (victimSide != null) victimSide.anchoredPosition = Vector2.Lerp(vicStartPos, _victimOriginalPos, t);

            if (actionTextSide != null)
            {
                float textT = Mathf.Clamp01((elapsed - 0.15f) / (animTime - 0.15f));
                actionTextSide.localScale = _actionOriginalScale * Mathf.SmoothStep(0f, 1f, textT);
            }

            yield return null;
        }

        if (attackerSide != null) attackerSide.anchoredPosition = _attackerOriginalPos;
        if (victimSide != null) victimSide.anchoredPosition = _victimOriginalPos;
        if (actionTextSide != null) actionTextSide.localScale = _actionOriginalScale;

        float waitElapsed = 0f;
        while (waitElapsed < duration && !_skipRequested)
        {
            waitElapsed += Time.deltaTime;
            yield return null;
        }

        HideSpotlight();
    }

    private void HideSpotlight()
    {
        if (vignetteDimGroup != null) vignetteDimGroup.gameObject.SetActive(false);
        if (spotlightCardImage != null) spotlightCardImage.gameObject.SetActive(false);
        if (versusGroup != null) versusGroup.SetActive(false);
        if (spotlightRoot != null) spotlightRoot.SetActive(false);
        gameObject.SetActive(false);
        activeSpotlightRoutine = null;
    }
}