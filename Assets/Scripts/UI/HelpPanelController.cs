using UnityEngine;
using UnityEngine.UI;

public class HelpPanelController : MonoBehaviour
{
    public static HelpPanelController Instance { get; private set; }

    [SerializeField] private GameObject helpPanelRoot;
    [SerializeField] private Button closeButton;

    private CanvasGroup canvasGroup;

    private void Awake()
    {
        Instance = this;

        if (helpPanelRoot == null) helpPanelRoot = gameObject;

        canvasGroup = helpPanelRoot.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = helpPanelRoot.AddComponent<CanvasGroup>();

        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;

        if (closeButton != null)
            closeButton.onClick.AddListener(CloseHelp);
    }

    public void OpenHelp()
    {
        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;
        canvasGroup.interactable = true;
        helpPanelRoot.transform.SetAsLastSibling();
    }

    public void CloseHelp()
    {
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
    }
}