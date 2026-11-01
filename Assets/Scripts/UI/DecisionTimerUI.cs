using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DecisionTimerUI : MonoBehaviour
{
    public static DecisionTimerUI Instance { get; private set; }

    [SerializeField] private GameObject visualRoot;
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private Image fillImage;
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color urgentColor = new Color(1f, 0.3f, 0.3f, 1f);
    [SerializeField] private float urgentBelowSeconds = 4f;

    private Coroutine routine;

    private void Awake()
    {
        Instance = this;
        if (visualRoot != null) visualRoot.SetActive(false);
    }

    public void Show(bool isLocalPlayer, string playerName, float seconds)
    {
        if (routine != null) StopCoroutine(routine);
        if (visualRoot != null) visualRoot.SetActive(true);
        routine = StartCoroutine(CountdownRoutine(isLocalPlayer ? "Your move" : playerName, seconds));
    }

    public void Hide()
    {
        if (routine != null) StopCoroutine(routine);
        routine = null;
        if (visualRoot != null) visualRoot.SetActive(false);
    }

    private IEnumerator CountdownRoutine(string label, float seconds)
    {
        float endTime = Time.unscaledTime + seconds;
        while (true)
        {
            float remaining = endTime - Time.unscaledTime;
            if (remaining <= 0f) break;

            Color c = remaining <= urgentBelowSeconds ? urgentColor : normalColor;
            if (timerText != null)
            {
                timerText.text = $"{label}: {Mathf.CeilToInt(remaining)}";
                timerText.color = c;
            }
            if (fillImage != null)
            {
                fillImage.fillAmount = Mathf.Clamp01(remaining / seconds);
                fillImage.color = c;
            }
            yield return null;
        }

        routine = null;
        if (visualRoot != null) visualRoot.SetActive(false);
    }
}