using UnityEngine;
using UnityEngine.UI;

public class DrawDeckClick : MonoBehaviour
{
    public static DrawDeckClick Instance { get; private set; }

    [SerializeField] private Outline deckGlowOutline;
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
}