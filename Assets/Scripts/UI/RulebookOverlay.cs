using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class RulebookController : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Image pageImageDisplay;
    [SerializeField] private TMP_Text pageNumText;
    [SerializeField] private Button nextBtn;
    [SerializeField] private Button prevBtn;
    [SerializeField] private Button closeBtn;

    [Header("Book Content")]
    [SerializeField] private Sprite[] rulePages;

    private int _currentPageIndex = 0;

    private void Awake()
    {
        if (nextBtn != null) nextBtn.onClick.AddListener(NextPage);
        if (prevBtn != null) prevBtn.onClick.AddListener(PrevPage);
        if (closeBtn != null) closeBtn.onClick.AddListener(CloseBook);
    }

    public void OpenBook()
    {
        gameObject.SetActive(true);
        _currentPageIndex = 0;
        UpdatePageDisplay();
    }

    public void CloseBook()
    {
        gameObject.SetActive(false);
    }

    private void NextPage()
    {
        if (rulePages == null || rulePages.Length == 0) return;

        if (_currentPageIndex < rulePages.Length - 1)
        {
            _currentPageIndex++;
            UpdatePageDisplay();
        }
    }

    private void PrevPage()
    {
        if (_currentPageIndex > 0)
        {
            _currentPageIndex--;
            UpdatePageDisplay();
        }
    }

    private void UpdatePageDisplay()
    {
        if (rulePages == null || rulePages.Length == 0 || pageImageDisplay == null) return;

        pageImageDisplay.sprite = rulePages[_currentPageIndex];

        if (pageNumText != null)
        {
            pageNumText.text = $"Page {_currentPageIndex + 1} of {rulePages.Length}";
        }

        if (prevBtn != null) prevBtn.interactable = _currentPageIndex > 0;
        if (nextBtn != null) nextBtn.interactable = _currentPageIndex < rulePages.Length - 1;
    }
}