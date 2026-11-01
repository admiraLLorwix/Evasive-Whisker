using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;
using Unity.Netcode;

public class DetectiveCatUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public static DetectiveCatUI Instance { get; private set; }

    [SerializeField] private TMP_Text badgeText;
    [SerializeField] private GameObject readyGlow;
    [SerializeField] private Color readyColor = new Color(0.3f, 1f, 0.4f, 1f);
    [SerializeField] private Color idleColor = new Color(1f, 1f, 1f, 0.85f);

    private bool hasCat;
    private DetectiveCatType cat;
    private int lastUsedRound = 0;
    private RectTransform rectTransform;
    private Canvas canvas;
    private Vector2 originalPosition;
    private Transform originalParent;
    private int originalSiblingIndex;
    private bool dragging;

    private string lastBadge;
    private bool lastReady;

    private void Awake()
    {
        Instance = this;
        rectTransform = GetComponent<RectTransform>();
        canvas = GetComponentInParent<Canvas>();
        if (readyGlow != null) readyGlow.SetActive(false);
        if (badgeText != null) badgeText.gameObject.SetActive(false);
    }

    public void SetCat(DetectiveCatType catType)
    {
        cat = catType;
        hasCat = true;
        lastUsedRound = 0;
    }

    public void SetLastUsedRound(int round) => lastUsedRound = round;

    private bool InUsablePhase()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null || NetworkManager.Singleton == null) return false;
        if (gm.ActiveTurnPlayerId.Value != NetworkManager.Singleton.LocalClientId) return false;

        switch (DetectiveCatRules.Timing(cat))
        {
            case CatTiming.DrawPhase: return gm.CurrentState.Value == GameState.PlayerTurn_Draw;
            case CatTiming.ActionPhase: return gm.CurrentState.Value == GameState.PlayerTurn_Action;
            default: return false;
        }
    }
    private bool EvaluateReady(out string badge)
    {
        badge = null;
        if (!hasCat || GameManager.Instance == null) return false;

        int round = GameManager.Instance.CurrentRound.Value;
        string reason = DetectiveCatRules.UnavailableReason(cat, round, lastUsedRound);
        if (reason != null)
        {
            badge = DetectiveCatRules.Timing(cat) == CatTiming.Passive ? "Passive" : reason;
            return false;
        }

        if (!InUsablePhase())
        {
            badge = DetectiveCatRules.PhaseHint(cat);
            return false;
        }

        badge = "READY - drag up";
        return true;
    }
    private void Update()
    {
        bool ready = EvaluateReady(out string badge);
        if (ready == lastReady && badge == lastBadge) return;
        lastReady = ready;
        lastBadge = badge;

        if (readyGlow != null) readyGlow.SetActive(ready);
        if (badgeText != null)
        {
            badgeText.gameObject.SetActive(!string.IsNullOrEmpty(badge));
            badgeText.text = badge;
            badgeText.color = ready ? readyColor : idleColor;
        }
    }




    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!EvaluateReady(out string badge))
        {
            eventData.pointerDrag = null;
            if (!string.IsNullOrEmpty(badge))
                TurnActionUI.Instance?.ShowTemporaryNotification($"<color=red>{DetectiveCatRules.DisplayName(cat)}: {badge}</color>", 2.5f);
            return;
        }

        dragging = true;
        originalPosition = rectTransform.anchoredPosition;
        originalParent = transform.parent;
        originalSiblingIndex = transform.GetSiblingIndex();
        if (canvas != null) transform.SetParent(canvas.transform, true);
    }
    public void OnDrag(PointerEventData eventData)
    {
        if (!dragging || canvas == null) return;
        rectTransform.anchoredPosition += eventData.delta / canvas.scaleFactor;
    }
    public void OnEndDrag(PointerEventData eventData)
    {
        if (!dragging) return;
        dragging = false;

        bool draggedUp = transform.position.y > Screen.height * 0.35f;
        ReturnHome();

        if (draggedUp) GameManager.Instance?.ActivateDetectiveServerRpc();
    }
    private void ReturnHome()
    {
        if (originalParent == null) return;
        transform.SetParent(originalParent, true);
        transform.SetSiblingIndex(originalSiblingIndex);
        rectTransform.anchoredPosition = originalPosition;
    }
}