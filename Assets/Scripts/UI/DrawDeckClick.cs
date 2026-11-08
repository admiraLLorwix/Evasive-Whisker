using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class DrawDeckClick : MonoBehaviour
{
    public static DrawDeckClick Instance { get; private set; }

    [SerializeField] private Outline deckGlowOutline;
    [SerializeField] private TMP_Text deckRemainingCountText;
    [SerializeField] private Color glowColor = new Color(0.2f, 1f, 0.4f, 1f);

    private Coroutine glowPulseRoutine;
    private Button button;

    private void Awake()
    {
        Instance = this;
        button = GetComponent<Button>();
        if (deckGlowOutline == null) deckGlowOutline = GetComponent<Outline>();

        if (button != null)
        {
            button.onClick.AddListener(OnDeckClicked);
        }

        SetGlow(false);
    }

    public void SetGlow(bool enable)
    {
        //if (deckGlowOutline != null) deckGlowOutline.enabled = enable;
        if (button != null) button.interactable = enable;

        if (deckGlowOutline == null) return;

        if (glowPulseRoutine != null)
        {
            StopCoroutine(glowPulseRoutine);
            glowPulseRoutine = null;
        }

        if (enable)
        {
            deckGlowOutline.enabled = true;
            deckGlowOutline.effectColor = glowColor;
            glowPulseRoutine = StartCoroutine(PulseGlowRoutine());
        }
        else
        {
            deckGlowOutline.enabled = false;
        }
    }

    private IEnumerator PulseGlowRoutine()
    {
        while (true)
        {
            float lerp = (Mathf.Sin(Time.time * 5f) + 1f) / 2f;
            Color c = glowColor;
            c.a = Mathf.Lerp(0.3f, 1f, lerp);
            deckGlowOutline.effectColor = c;
            yield return null;
        }
    }

    private void OnDeckClicked()
    {
        SetGlow(false);
        GameManager.Instance?.RequestDrawCardServerRpc();
    }
    public void UpdateRemainingCount(int count)
    {
        if (deckRemainingCountText != null)
        {
            deckRemainingCountText.text = $"{count}";
        }
    }
}