using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;
using TMPro;

public class DraftPanelController : MonoBehaviour
{
    public static DraftPanelController Instance { get; private set; }

    [Header("UI References")]
    [SerializeField] private GameObject draftModal;
    [SerializeField] private TMP_Text draftInstructionsText;
    [SerializeField] private Transform cardContainer;
    [SerializeField] private GameObject draftCardButtonPrefab;

    private void Awake()
    {
        Instance = this;
        if (draftModal != null) draftModal.SetActive(false);
    }

    public void OpenDraftSelection(DetectiveCatType[] availableCats)
    {
        if (draftModal == null || cardContainer == null || draftCardButtonPrefab == null)
        {
            Debug.LogError("[DraftPanel] Missing inspector");
            return;
        }

        draftModal.SetActive(true);
        if (draftInstructionsText != null)
        {
            draftInstructionsText.text = $"Choose your Detective Cat ({availableCats.Length} remaining)";
        }

        foreach (Transform child in cardContainer)
        {
            Destroy(child.gameObject);
        }

        List<DetectiveCatType> randomizedPool = new List<DetectiveCatType>(availableCats);
        for (int i = 0; i < randomizedPool.Count; i++)
        {
            int rnd = Random.Range(i, randomizedPool.Count);
            var temp = randomizedPool[i];
            randomizedPool[i] = randomizedPool[rnd];
            randomizedPool[rnd] = temp;
        }

        for (int i = 0; i < randomizedPool.Count; i++)
        {
            DetectiveCatType catToPick = randomizedPool[i];
            GameObject btnObj = Instantiate(draftCardButtonPrefab, cardContainer);
            Button btn = btnObj.GetComponent<Button>();

            btn.onClick.AddListener(() => OnCatSelected(catToPick));
        }
    }

    private void OnCatSelected(DetectiveCatType chosenCat)
    {
        draftModal.SetActive(false);

        GameManager.Instance.SubmitChosenCatServerRpc(chosenCat);
        Debug.Log($"[Draft] You chose: {chosenCat}");
    }
}