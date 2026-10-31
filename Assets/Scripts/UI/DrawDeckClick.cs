using UnityEngine;
using UnityEngine.UI;

public class DrawDeckClick : MonoBehaviour
{
    public static DrawDeckClick Instance { get; private set; }

    [SerializeField] private Outline deckGlowOutline;
    [SerializeField] private TMPro.TMP_Text deckRemainingCountText;
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
        if (deckGlowOutline != null) deckGlowOutline.enabled = enable;
        if (button != null) button.interactable = enable;
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