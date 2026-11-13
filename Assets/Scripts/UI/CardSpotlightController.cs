using EvasiveWhisker.Cards;
using System.Collections;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CardSpotlightController : MonoBehaviour, IPointerClickHandler
{
    public static CardSpotlightController Instance { get; private set; }

    [SerializeField] private GameObject spotlightRoot; [SerializeField] private GameObject cardRevealGroup;
    [SerializeField] private Image spotlightCardImage; //[SerializeField] private Sprite defaultCardBackSprite;
    public Sprite defaultCardBackSprite;
    //[SerializeField] private TMP_Text actionTitleText;
    [SerializeField] private Outline cardGlowOutline;
    //optional but better if set
    //necessary lang para di matakpan player hand
    [SerializeField] private Transform[] keepInFront;
    [SerializeField] private CanvasGroup vignetteDimGroup;
    [SerializeField] private Image vignetteImage;


    //vignette
    [SerializeField] private Color abilityColor = new Color(0.2f, 0.6f, 1f, 1f);
    [SerializeField] private Color trapColor = new Color(0.6f, 0.4f, 0.2f, 1f);
    [SerializeField] private Color custodyColor = new Color(0.2f, 1f, 0.4f, 1f);
    [SerializeField] private Color whiskerblowerColor = new Color(1f, 0.85f, 0.2f, 1f);
    [SerializeField] private Color counterplayColor = new Color(1f, 0.5f, 0f, 1f);
    [SerializeField] private Color defaultColor = new Color(0f, 0f, 0f, 1f);



    [SerializeField] private Image versusCardImage;
    [SerializeField] private TMP_Text versusTimerText;
    [SerializeField] private GameObject versusGroup;
    [SerializeField] private RectTransform attackerSide; [SerializeField] private Image attackerAvatar; [SerializeField] private TMP_Text versusAttackerText;
    [SerializeField] private RectTransform victimSide; [SerializeField] private Image victimAvatar; [SerializeField] private TMP_Text versusVictimText;
    [SerializeField] private RectTransform actionTextSide; [SerializeField] private TMP_Text versusActionText;
    [SerializeField] private RectTransform canvasRoot;
    [SerializeField] private float shakeDuration = 0.15f;
    [SerializeField] private float shakeMagnitude = 8f;
    [SerializeField] private CanvasGroup headerBannerGroup;
    [SerializeField] private Image headerAvatar;
    [SerializeField] private TMP_Text headerPlayerName;
    [SerializeField] private TMP_Text headerActionText;
    private ulong _currentAttackerId;
    private ulong _currentVictimId;

    private Coroutine activeSpotlightRoutine;
    private bool _skipRequested = false;
    private Vector2 _canvasOriginalPos;
    private Vector2 _attackerOriginalPos;
    private Vector2 _victimOriginalPos;
    private Vector3 _actionOriginalScale; [SerializeField] private float cardBaseScale = 0.85f;
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

        if (vignetteImage != null)
        {
            RectTransform rt = vignetteImage.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;

            GenerateVignetteTexture();
        }
    }

    private void GenerateVignetteTexture()
    {
        int size = 256;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[size * size];
        Vector2 center = new Vector2(size / 2f, size / 2f);
        float maxDistance = size * 0.7f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center);
                float alpha = Mathf.Clamp01(dist / maxDistance);
                alpha = Mathf.SmoothStep(0f, 1f, alpha);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        vignetteImage.sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.clickCount == 2) _skipRequested = true;
    }

    public void PlayCatSpotlight(DetectiveCatType cat, ulong playerId, string bannerMessage, Color glowColor, float duration = 2.5f)
    {
        Sprite sprite = CardDatabase.Instance != null ? CardDatabase.Instance.GetLocalHandCardSprite(cat) : null;
        PlaySpotlight("", playerId, bannerMessage, glowColor, duration, false, sprite);
    }

    public void PlaySpotlight(string cardId, ulong playerId, string actionMessage, Color glowColor, float duration = 3f, bool showBackface = false, Sprite overrideSprite = null)
    {
        if (activeSpotlightRoutine != null) StopCoroutine(activeSpotlightRoutine);
        _skipRequested = false;
        gameObject.SetActive(true);
        if (spotlightRoot != null) spotlightRoot.SetActive(true);

        if (cardRevealGroup != null) cardRevealGroup.SetActive(true);
        if (versusGroup != null) versusGroup.SetActive(false);
        UpdateHeaderBanner(playerId, actionMessage);
        UpdateVignetteColor(cardId);

        transform.SetAsLastSibling();
        if (keepInFront != null)
        {
            foreach (Transform front in keepInFront)
                if (front != null && front.parent == transform.parent) front.SetAsLastSibling();
        }

        activeSpotlightRoutine = StartCoroutine(CardRevealRoutine(cardId, actionMessage, glowColor, duration, showBackface, overrideSprite));
    }

    private void UpdateVignetteColor(string cardId)
    {
        Debug.Log($"<color=yellow>[sl] upd vig: {cardId}</color>");

        if (vignetteImage == null)
        {
            Debug.LogError("[sl] !vigimg");
            return;
        }

        Color targetColor = defaultColor;

        if (!string.IsNullOrEmpty(cardId) && CardDatabase.Instance != null)
        {
            CardType? type = CardDatabase.Instance.GetCardType(cardId);

            if (type.HasValue) { 
                switch (type.Value)
                {
                    case CardType.Ability: targetColor = abilityColor; break;
                    case CardType.Trap: targetColor = trapColor; break;
                    case CardType.Custody: targetColor = custodyColor; break;
                    case CardType.WhiskerBlower: targetColor = whiskerblowerColor; break;
                    case CardType.Counterplay: targetColor = counterplayColor; break;
                    default: targetColor = defaultColor; break;
                }
            }
            else
            {
                Debug.LogWarning($"[sl] no card id {cardId}");
            }
        }
        else
        {
            Debug.LogWarning("[sl] cardid null");
        }

        vignetteImage.color = targetColor;
        Debug.Log($"<color=cyan>[sl] vigcol - {targetColor}</color>");
    }

    private void UpdateHeaderBanner(ulong playerId, string actionMessage)
    {
        if (headerBannerGroup != null)
        {
            headerBannerGroup.gameObject.SetActive(true);
            headerBannerGroup.alpha = 1f;
        }

        if (GameManager.Instance != null && CardDatabase.Instance != null)
        {
            string pName = GameManager.Instance.GetPlayerName(playerId);
            DetectiveCatType cat = GameManager.Instance.GetPlayerDetective(playerId);
            Sprite sprite = CardDatabase.Instance.GetIslandProfileSprite(cat);

            if (headerPlayerName != null) headerPlayerName.text = pName;
            if (headerAvatar != null && sprite != null) headerAvatar.sprite = sprite;
            if (headerActionText != null) headerActionText.text = actionMessage;
        }
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
        if (cardGlowOutline != null) cardGlowOutline.effectColor = glowColor;

        RectTransform cardRT = spotlightCardImage.rectTransform;
        Vector2 finalPos = Vector2.zero;
        Vector2 startPos = finalPos + new Vector2(0, 500f);
        float startRotZ = Random.Range(-10f, 10f);
        float endRotZ = Random.Range(-2f, 2f);
        Vector3 endScale = Vector3.one * cardBaseScale;
        Vector3 startScale = endScale * 1.3f;

        cardRT.anchoredPosition = startPos;
        cardRT.localScale = startScale;

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

            float curveT = t * t;
            cardRT.anchoredPosition = Vector2.Lerp(startPos, finalPos, curveT);
            cardRT.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(startRotZ, endRotZ, curveT));
            cardRT.localScale = Vector3.Lerp(startScale, endScale, curveT);

            yield return null;
        }

        cardRT.anchoredPosition = finalPos;
        cardRT.localRotation = Quaternion.Euler(0, 0, endRotZ);
        cardRT.localScale = endScale;

        if (canvasRoot != null) StartCoroutine(ScreenShakeRoutine());

        float elapsed = dropDuration;
        while (elapsed < duration && !_skipRequested)
        {
            elapsed += Time.deltaTime;
            float pulse = 1.0f + (Mathf.Sin(elapsed * 4.5f) * 0.015f);
            cardRT.localScale = endScale * pulse;
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

    public void PlayVersusSpotlight(ulong attackerId, ulong victimId, string actionText, string cardId = "", bool showTimer = true)
    {
        if (activeSpotlightRoutine != null) StopCoroutine(activeSpotlightRoutine);
        _skipRequested = false;
        gameObject.SetActive(true);
        if (spotlightRoot != null) spotlightRoot.SetActive(true);

        if (cardRevealGroup != null) cardRevealGroup.SetActive(false);
        if (versusGroup != null) versusGroup.SetActive(true);
        transform.SetAsLastSibling();
        string attackerName = GameManager.Instance != null ? GameManager.Instance.GetPlayerName(attackerId) : "Player 1";
        string victimName = GameManager.Instance != null ? GameManager.Instance.GetPlayerName(victimId) : "Player 2";

        if (versusAttackerText != null) versusAttackerText.text = attackerName;
        if (versusVictimText != null) versusVictimText.text = victimName;
        _currentAttackerId = attackerId;
        _currentVictimId = victimId;
        if (versusActionText != null) versusActionText.text = actionText;
        if (versusCardImage != null)
        {
            if (!string.IsNullOrEmpty(cardId) && CardDatabase.Instance != null)
            {
                Sprite cardSprite = CardDatabase.Instance.GetCardSprite(cardId);
                versusCardImage.sprite = cardSprite;
                versusCardImage.gameObject.SetActive(cardSprite != null);
            }
            else
            {
                versusCardImage.gameObject.SetActive(false);
            }
        }

        if (GameManager.Instance != null && CardDatabase.Instance != null)
        {
            DetectiveCatType atkCat = GameManager.Instance.GetPlayerDetective(attackerId);
            DetectiveCatType vicCat = GameManager.Instance.GetPlayerDetective(victimId);
            Sprite atkSprite = CardDatabase.Instance.GetIslandProfileSprite(atkCat);
            Sprite vicSprite = CardDatabase.Instance.GetIslandProfileSprite(vicCat);

            if (attackerAvatar != null)
            {
                attackerAvatar.sprite = atkSprite;
                attackerAvatar.color = Color.white;
            }
            if (victimAvatar != null)
            {
                victimAvatar.sprite = vicSprite;
                victimAvatar.color = Color.white;
            }
        }
        if (versusTimerText != null) versusTimerText.gameObject.SetActive(showTimer);
        if (vignetteImage != null) vignetteImage.color = defaultColor;
        activeSpotlightRoutine = StartCoroutine(VersusRoutine(10f, showTimer));
    }

    private IEnumerator VersusRoutine(float duration, bool showTimer)
    {
        if (vignetteDimGroup != null)
        {
            vignetteDimGroup.gameObject.SetActive(true);
            vignetteDimGroup.alpha = 0.85f;
        }

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
        while (waitElapsed < duration)
        {
            waitElapsed += Time.deltaTime;

            if (showTimer && versusTimerText != null)
            {
                float remaining = duration - waitElapsed;
                versusTimerText.text = Mathf.CeilToInt(remaining).ToString();
                versusTimerText.color = remaining <= 3f ? Color.red : Color.white;
            }
            yield return null;
        }
        HideSpotlight();
    }
    public void StopVersusTimerEarly()
    {
        if (activeSpotlightRoutine != null) StopCoroutine(activeSpotlightRoutine);
        HideSpotlight();
    }

    private void HideSpotlight()
    {
        if (headerBannerGroup != null) headerBannerGroup.gameObject.SetActive(false);
        if (vignetteDimGroup != null) vignetteDimGroup.gameObject.SetActive(false);
        if (spotlightCardImage != null) spotlightCardImage.gameObject.SetActive(false);
        if (versusGroup != null) versusGroup.SetActive(false);
        if (spotlightRoot != null) spotlightRoot.SetActive(false);
        gameObject.SetActive(false);
        activeSpotlightRoutine = null;
    }


    //addtl effects
    public void PlayStealResultSequence(ulong thiefId, ulong victimId, string cardId)
    {
        gameObject.SetActive(true);

        bool isInvolved = NetworkManager.Singleton.LocalClientId == thiefId || NetworkManager.Singleton.LocalClientId == victimId;

        string displayCardId = isInvolved ? cardId : "";

        StartCoroutine(StealResultRoutine(thiefId, victimId, displayCardId));
    }

    private IEnumerator StealResultRoutine(ulong thiefId, ulong victimId, string cardId)
    {
        PlayVersusSpotlight(thiefId, victimId, "stole a card from", cardId, false);
        yield return new WaitForSeconds(0.5f);

        if (victimAvatar != null)
        {
            Vector2 originalPos = victimAvatar.rectTransform.anchoredPosition;
            Color originalColor = victimAvatar.color;
            victimAvatar.color = Color.red;

            float shakeTime = 0.8f;
            float elapsed = 0f;
            while (elapsed < shakeTime)
            {
                elapsed += Time.deltaTime;
                float x = Random.Range(-15f, 15f);
                victimAvatar.rectTransform.anchoredPosition = originalPos + new Vector2(x, 0f);
                yield return null;
            }

            victimAvatar.rectTransform.anchoredPosition = originalPos;
            victimAvatar.color = originalColor;
        }

        if (CardFlightManager.Instance != null && attackerAvatar != null && victimAvatar != null)
        {
            Sprite sprite = null;
            if (!string.IsNullOrEmpty(cardId) && CardDatabase.Instance != null)
                sprite = CardDatabase.Instance.GetCardSprite(cardId);

            if (sprite == null && CardSpotlightController.Instance != null)
                sprite = CardSpotlightController.Instance.defaultCardBackSprite;

            Vector3 startPos = victimAvatar.transform.position;
            Vector3 endPos = attackerAvatar.transform.position;
            CardFlightManager.Instance.FlyCard(startPos, endPos, sprite, null, 1.0f);
        }

        yield return new WaitForSeconds(1.8f);
        HideSpotlight();
    }

    public void PlayHurtAnimation(ulong playerId)
    {
        gameObject.SetActive(true);
        if (spotlightRoot != null) spotlightRoot.SetActive(true);
        if (cardRevealGroup != null) cardRevealGroup.SetActive(false);
        if (versusGroup != null) versusGroup.SetActive(true);
        if (vignetteDimGroup != null) vignetteDimGroup.gameObject.SetActive(true);
        if (vignetteImage != null) vignetteImage.color = defaultColor;

        if (GameManager.Instance != null && CardDatabase.Instance != null)
        {
            DetectiveCatType atkCat = GameManager.Instance.GetPlayerDetective(_currentAttackerId);
            DetectiveCatType vicCat = GameManager.Instance.GetPlayerDetective(_currentVictimId);
            if (attackerAvatar != null) attackerAvatar.sprite = CardDatabase.Instance.GetIslandProfileSprite(atkCat);
            if (victimAvatar != null) victimAvatar.sprite = CardDatabase.Instance.GetIslandProfileSprite(vicCat);
        }

        StartCoroutine(HurtRoutine(playerId));
    }

    private IEnumerator HurtRoutine(ulong playerId)
    {
        Image targetAvatar = null;
        if (playerId == _currentAttackerId) targetAvatar = attackerAvatar;
        else if (playerId == _currentVictimId) targetAvatar = victimAvatar;

        if (targetAvatar != null)
        {
            Vector2 originalPos = targetAvatar.rectTransform.anchoredPosition;
            Color originalColor = targetAvatar.color;
            targetAvatar.color = Color.red;

            float shakeTime = 0.8f;
            float elapsed = 0f;

            while (elapsed < shakeTime)
            {
                elapsed += Time.deltaTime;
                float x = Random.Range(-15f, 15f);
                targetAvatar.rectTransform.anchoredPosition = originalPos + new Vector2(x, 0f);
                yield return null;
            }

            targetAvatar.rectTransform.anchoredPosition = originalPos;
            targetAvatar.color = originalColor;
        }
    }
}