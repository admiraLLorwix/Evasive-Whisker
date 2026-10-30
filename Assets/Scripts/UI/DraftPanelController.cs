using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;
using TMPro;

public class DraftPanelController : MonoBehaviour
{
    public static DraftPanelController Instance { get; private set; }

    [SerializeField] private GameObject draftPanelRoot;
    [SerializeField] private GameObject chooserModalWindow;
    [SerializeField] private GameObject waitingModalWindow;

    [SerializeField] private TMP_Text draftTitleText;
    [SerializeField] private Transform cardGridContainer;
    [SerializeField] private GameObject draftCardButtonPrefab;

    [SerializeField] private TMP_Text waitingText;

    private void Awake()
    {
        Instance = this;
    }

    public void OpenDraftForChooser(DetectiveCatType[] availableCats)
    {
        Debug.Log($"<color=green>[DraftPanel] {availableCats.Length} cats available.</color>");

        gameObject.SetActive(true);
        if (draftPanelRoot != null) draftPanelRoot.SetActive(true);
        if (chooserModalWindow != null) chooserModalWindow.SetActive(true);
        if (waitingModalWindow != null) waitingModalWindow.SetActive(false);

        if (draftTitleText != null)
        {
            draftTitleText.text = $"Choose your Detective Cat ({availableCats.Length} remaining)";
        }

        if (cardGridContainer == null || draftCardButtonPrefab == null)
        {
            Debug.LogError("[DraftPanel] !cardgridcontatiner // !drawcardbtnprefab");
            return;
        }

        foreach (Transform child in cardGridContainer)
        {
            Destroy(child.gameObject);
        }

        List<DetectiveCatType> pool = new List<DetectiveCatType>(availableCats);
        for (int i = 0; i < pool.Count; i++)
        {
            int rnd = Random.Range(i, pool.Count);
            var temp = pool[i];
            pool[i] = pool[rnd];
            pool[rnd] = temp;
        }

        for (int i = 0; i < pool.Count; i++)
        {
            DetectiveCatType catToPick = pool[i];
            GameObject btnObj = Instantiate(draftCardButtonPrefab, cardGridContainer);
            Button btn = btnObj.GetComponent<Button>();

            btn.onClick.AddListener(() => OnCatSelected(catToPick));
        }
    }

    public void OpenDraftSelection(DetectiveCatType[] cats) => OpenDraftForChooser(cats);

    public void ShowWaitingForChooser(string chooserPlayerName)
    {
        Debug.Log($"[DraftPanel] waiting for chooser: {chooserPlayerName}");

        gameObject.SetActive(true);
        if (draftPanelRoot != null) draftPanelRoot.SetActive(true);
        if (chooserModalWindow != null) chooserModalWindow.SetActive(false);
        if (waitingModalWindow != null) waitingModalWindow.SetActive(true);

        if (waitingText != null)
        {
            waitingText.text = $"Waiting for <color=yellow>{chooserPlayerName}</color> to choose a Detective Cat...";
        }
    }

    private void OnCatSelected(DetectiveCatType chosenCat)
    {
        HideAll();
        GameManager.Instance.SubmitChosenCatServerRpc(chosenCat);
    }

    public void HideAll()
    {
        if (draftPanelRoot != null) draftPanelRoot.SetActive(false);
        if (chooserModalWindow != null) chooserModalWindow.SetActive(false);
        if (waitingModalWindow != null) waitingModalWindow.SetActive(false);
    }
}