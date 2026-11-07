using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class ActionLogUI : MonoBehaviour
{
    public static ActionLogUI Instance { get; private set; }
    [SerializeField] private Button toggleButton;
    [SerializeField] private GameObject logPanel;
    [SerializeField] private TMP_Text logText;
    [SerializeField] private ScrollRect scrollRect;

    private bool isOpen = false;

    private void Awake()
    {
        Instance = this;

        if (logPanel != null) logPanel.SetActive(false);
        if (toggleButton != null) toggleButton.onClick.AddListener(ToggleLog);
    }

    public void ToggleLog()
    {
        isOpen = !isOpen;
        if (logPanel != null) logPanel.SetActive(isOpen);
    }

    public void AddLog(string message)
    {
        if (logText == null) return;

        logText.text += $"\n> {message}";

        if (logText.text.Length > 3000)
            logText.text = logText.text.Substring(1500);

        if (scrollRect != null)
        {
            Canvas.ForceUpdateCanvases();
            scrollRect.verticalNormalizedPosition = 0f;
        }
    }
}