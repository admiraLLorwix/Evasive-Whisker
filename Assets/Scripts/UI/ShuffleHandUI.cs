using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ShuffleHandUI : MonoBehaviour
{
    public static ShuffleHandUI Instance { get; private set; }

    [SerializeField] private GameObject visualRoot;
    [SerializeField] private Button shuffleButton; [SerializeField] private TMP_Text chargeText;
    //not sure yet kung san ilalagay / can be empty
    [SerializeField] private TMP_Text reasonText; [SerializeField] private GameObject lockedBadge;
    [SerializeField] private Color availableColor = Color.black;
    [SerializeField] private Color unavailableColor = new Color(0.5f, 0.5f, 0.5f, 1f);
    [SerializeField] private Color blockedFlashColor = new Color(1f, 0.3f, 0.3f, 1f);
    [SerializeField] private float shakeDuration = 0.3f;
    [SerializeField] private float shakeStrength = 9f;
    [SerializeField] private float reasonDuration = 2f;

    private int charges = 0;
    private bool locked = false;
    private bool stateReceived = false;

    private bool lastShown;
    private bool lastAvailable;
    private bool visualsDirty = true;
    private float ignoreClicksUntil = 0f;

    private Coroutine shakeRoutine;
    private Coroutine reasonRoutine;
    private Vector2 buttonBasePos;
    private bool hasBasePos = false;

    private void Awake()
    {
        Instance = this;

        if (shuffleButton != null)
        {
            shuffleButton.interactable = true;
            shuffleButton.onClick.RemoveAllListeners();
            shuffleButton.onClick.AddListener(OnShuffleClicked);
        }

        if (reasonText != null) reasonText.gameObject.SetActive(false);
        if (lockedBadge != null) lockedBadge.SetActive(false);
        if (visualRoot != null) visualRoot.SetActive(false);
        lastShown = false;
    }
    public void SetState(int newCharges, bool isLocked)
    {
        charges = newCharges;
        locked = isLocked;
        stateReceived = true;
        visualsDirty = true;
    }

    private void Update()
    {
        bool gameOver = GameManager.Instance != null && GameManager.Instance.CurrentState.Value == GameState.GameOver;
        bool shouldShow = stateReceived && !gameOver;

        if (shouldShow != lastShown)
        {
            lastShown = shouldShow;
            if (visualRoot != null) visualRoot.SetActive(shouldShow);
            visualsDirty = true;
        }
        if (!shouldShow) return;

        bool available = GetBlockReason() == null;
        if (available != lastAvailable) visualsDirty = true;

        if (visualsDirty)
        {
            lastAvailable = available;
            visualsDirty = false;
            ApplyVisuals(available);
        }
    }

    private void ApplyVisuals(bool available)
    {
        if (shuffleButton != null && shuffleButton.targetGraphic != null && shakeRoutine == null)
            shuffleButton.targetGraphic.color = available ? availableColor : unavailableColor;

        if (chargeText != null)
        {
            chargeText.text = $"{charges}/{GameManager.MaxShuffleCharges}";
            chargeText.color = (charges > 0 && !locked) ? availableColor : unavailableColor;
        }

        if (lockedBadge != null) lockedBadge.SetActive(locked);
    }
    private string GetBlockReason()
    {
        if (locked) return "You're being targeted - can't shuffle right now";

        if (PlayerHandController.Instance != null)
        {
            if (PlayerHandController.Instance.IsScatterPickActive) return "Finish passing your card first";
            if (PlayerHandController.Instance.HandCount < 2) return "You need at least 2 cards to shuffle";
        }

        if (charges <= 0) return "No shuffles left this turn cycle";
        return null;
    }

    private void OnShuffleClicked()
    {
        if (Time.unscaledTime < ignoreClicksUntil) return;

        string reason = GetBlockReason();
        Debug.Log($"[shuffle][ui] clicked. charges={charges}, locked={locked}, blockReason={(reason ?? "none")}, gm={(GameManager.Instance != null)}");

        if (reason != null)
        {
            ShowBlocked($"<color=red>{reason}</color>");
            return;
        }

        ignoreClicksUntil = Time.unscaledTime + 0.35f;
        GameManager.Instance?.RequestShuffleHandServerRpc();
    }
    public void ShowBlocked(string message)
    {
        if (shakeRoutine != null) StopCoroutine(shakeRoutine);
        shakeRoutine = StartCoroutine(ShakeRoutine());

        if (reasonText != null)
        {
            if (reasonRoutine != null) StopCoroutine(reasonRoutine);
            reasonRoutine = StartCoroutine(ReasonRoutine(message));
        }
        else
        {
            TurnActionUI.Instance?.ShowTemporaryNotification(message, 2.5f);
        }
    }

    private IEnumerator ShakeRoutine()
    {
        RectTransform rt = shuffleButton != null ? shuffleButton.transform as RectTransform : null;
        if (rt == null) { shakeRoutine = null; yield break; }

        if (!hasBasePos) { buttonBasePos = rt.anchoredPosition; hasBasePos = true; }
        rt.anchoredPosition = buttonBasePos;

        Graphic g = shuffleButton.targetGraphic;
        if (g != null) g.color = blockedFlashColor;

        float t = 0f;
        while (t < shakeDuration)
        {
            t += Time.unscaledDeltaTime;
            float fade = 1f - Mathf.Clamp01(t / shakeDuration);
            float offset = Mathf.Sin(t * 70f) * shakeStrength * fade;
            rt.anchoredPosition = buttonBasePos + new Vector2(offset, 0f);
            yield return null;
        }

        rt.anchoredPosition = buttonBasePos;
        shakeRoutine = null;
        visualsDirty = true;
    }

    private IEnumerator ReasonRoutine(string message)
    {
        reasonText.gameObject.SetActive(true);
        reasonText.text = message;

        Color c = reasonText.color;
        c.a = 1f;
        reasonText.color = c;

        yield return new WaitForSecondsRealtime(reasonDuration);

        float fade = 0.35f;
        float t = 0f;
        while (t < fade)
        {
            t += Time.unscaledDeltaTime;
            c.a = 1f - Mathf.Clamp01(t / fade);
            reasonText.color = c;
            yield return null;
        }

        reasonText.gameObject.SetActive(false);
        reasonRoutine = null;
    }

    private void OnDisable()
    {
        if (hasBasePos && shuffleButton != null)
        {
            RectTransform rt = shuffleButton.transform as RectTransform;
            if (rt != null) rt.anchoredPosition = buttonBasePos;
        }
        shakeRoutine = null;
        reasonRoutine = null;
    }
}