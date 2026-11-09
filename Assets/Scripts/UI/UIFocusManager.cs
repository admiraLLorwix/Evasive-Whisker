using UnityEngine;

public enum FocusTarget { None, DrawDeck, Opponents, Hand }

public class UIFocusManager : MonoBehaviour
{
    public static UIFocusManager Instance { get; private set; }

    [SerializeField] private GameObject globalDimPanel;
    [SerializeField] private Canvas drawDeckCanvas;
    [SerializeField] private Canvas opponentsCanvas;
    [SerializeField] private Canvas localHandCanvas;

    private void Awake()
    {
        Instance = this;
        if (globalDimPanel != null) globalDimPanel.SetActive(false);
    }

    public void FocusOn(FocusTarget target)
    {
        if (globalDimPanel != null) globalDimPanel.SetActive(true);

        if (drawDeckCanvas != null) drawDeckCanvas.sortingOrder = 0;
        if (opponentsCanvas != null) opponentsCanvas.sortingOrder = 0;
        if (localHandCanvas != null) localHandCanvas.sortingOrder = 0;

        switch (target)
        {
            case FocusTarget.DrawDeck:
                if (drawDeckCanvas != null) drawDeckCanvas.sortingOrder = 10;
                break;
            case FocusTarget.Opponents:
                if (opponentsCanvas != null) opponentsCanvas.sortingOrder = 10;
                break;
            case FocusTarget.Hand:
                if (localHandCanvas != null) localHandCanvas.sortingOrder = 10;
                break;
        }
    }

    public void ClearFocus()
    {
        if (globalDimPanel != null) globalDimPanel.SetActive(false);

        if (drawDeckCanvas != null) drawDeckCanvas.sortingOrder = 0;
        if (opponentsCanvas != null) opponentsCanvas.sortingOrder = 0;
        if (localHandCanvas != null) localHandCanvas.sortingOrder = 0;
    }
}