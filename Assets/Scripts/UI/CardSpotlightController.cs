using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using EvasiveWhisker.Cards;

public class CardSpotlightController : MonoBehaviour
{
    public static CardSpotlightController Instance { get; private set; }

    [SerializeField] private GameObject spotlightRoot;
    [SerializeField] private Image spotlightCardImage; [SerializeField] private Sprite defaultCardBackSprite;
    [SerializeField] private TMP_Text actionTitleText;
    [SerializeField] private Outline cardGlowOutline;

    private Coroutine activeSpotlightRoutine;

    private void Awake()
    {
        Instance = this;
        if (spotlightRoot != null) spotlightRoot.SetActive(false);
    }

    public void PlaySpotlight(string cardId, string bannerMessage, Color glowColor, float duration = 2.8f, bool showBackface = false)
    {
        gameObject.SetActive(true);
        if (spotlightRoot != null) spotlightRoot.SetActive(true);
        transform.SetAsLastSibling();

        if (activeSpotlightRoutine != null) StopCoroutine(activeSpotlightRoutine);
        activeSpotlightRoutine = StartCoroutine(SpotlightAnimationRoutine(cardId, bannerMessage, glowColor, duration, showBackface));
    }

    private IEnumerator SpotlightAnimationRoutine(string cardId, string bannerMessage, Color glowColor, float duration, bool showBackface)
    {
        if (spotlightRoot == null) yield break;

        Transform dim = transform.Find("VignetteDim");
        if (dim != null) dim.gameObject.SetActive(true);

        Transform ribbon = transform.Find("BannerRibbon");
        if (ribbon != null) ribbon.gameObject.SetActive(true);

        if (spotlightCardImage != null) spotlightCardImage.gameObject.SetActive(true);

        Sprite cardSprite = null;

        if (showBackface)
        {
            cardSprite = defaultCardBackSprite;
        }
        else
        {
            if (CardDatabase.Instance != null) cardSprite = CardDatabase.Instance.GetCardSprite(cardId);
            if (cardSprite == null && CardDatabase.Instance != null)
            {
                Card cardSO = CardDatabase.Instance.GetCard(cardId);
                if (cardSO != null && cardSO.frontfaceFrame != null)
                {
                    cardSprite = Sprite.Create(cardSO.frontfaceFrame, new Rect(0, 0, cardSO.frontfaceFrame.width, cardSO.frontfaceFrame.height), new Vector2(0.5f, 0.5f));
                }
            }
        }

        if (spotlightCardImage != null)
        {
            spotlightCardImage.sprite = cardSprite != null ? cardSprite : defaultCardBackSprite;
            spotlightCardImage.color = Color.white;
        }

        if (actionTitleText != null) actionTitleText.text = bannerMessage;
        if (cardGlowOutline != null) cardGlowOutline.effectColor = glowColor;

        RectTransform cardRT = spotlightCardImage.rectTransform;

        float slamTimer = 0f;
        while (slamTimer < 0.22f)
        {
            slamTimer += Time.deltaTime;
            float t = slamTimer / 0.22f;
            cardRT.localScale = Vector3.Lerp(Vector3.one * 1.45f, Vector3.one, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }
        cardRT.localScale = Vector3.one;

        float elapsed = 0.22f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float pulse = 1.0f + (Mathf.Sin(elapsed * 4.5f) * 0.035f);
            cardRT.localScale = Vector3.one * pulse;
            yield return null;
        }

        if (dim != null) dim.gameObject.SetActive(false);
        if (ribbon != null) ribbon.gameObject.SetActive(false);
        if (spotlightCardImage != null) spotlightCardImage.gameObject.SetActive(false);
        spotlightRoot.SetActive(false);
        gameObject.SetActive(false);
        activeSpotlightRoutine = null;
    }
}